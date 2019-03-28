using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NLog;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public class Projection
    {
        private readonly ILog _logger;

        private IDisposable _connection = Disposable.Empty; 
        private readonly BufferBlock<IStream> _bufferBlock;
        private readonly ActionBlock<IStream> _actionBlock;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ConcurrentDictionary<string,int> _streams = new ConcurrentDictionary<string, int>();
        private readonly Dictionary<Type, Action<IEvent>> _handlers = new Dictionary<Type, Action<IEvent>>();
        private readonly Func<string,bool> _streamFilter = s => true;

        protected Projection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue)
        {
            _eventStore = eventStore;
            _logger = logger;
            _actionBlock = new ActionBlock<IStream>(Update,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                });
            _bufferBlock = new BufferBlock<IStream>();
            //_bufferBlock.LinkTo(DataflowBlock.NullTarget<IStream>());
            Rebuild();
            eventStore.Streams.Subscribe(async s => await Notify(s));
            messageQueue.Alerts.Where(s => s == "InvalidProjections").Subscribe(s => Rebuild());
        }

        protected void Register<TEvent>(Action<TEvent> when) where TEvent : class
        {
            _handlers.Add(typeof(TEvent), e => when(e as TEvent)); 
        }

        protected async Task Notify(IStream stream)
        {
            if (!_streamFilter(stream.Key))
                return;
            
            var version = stream.Version; 
            var projectionVersion = _streams.GetOrAdd(stream.Key,-1);
            _logger.Trace($"{stream.Key}@{projectionVersion}",this); 
            if (version > projectionVersion)
                await _bufferBlock.SendAsync(stream);
        }
        
        protected virtual void Reset() {}

        private void Pause()
        {
            _logger.Trace("", this);
            _connection.Dispose();
        }
        
        private void Unpause()
        {
            _logger.Trace("", this);
            _connection = _bufferBlock.LinkTo(_actionBlock);
        }
        
        private void Rebuild()
        {
            _logger.Trace("",this); 
            Pause();
            Reset();
            _eventStore.Events.Where(e => _streamFilter(e.Stream))
                .Finally(Unpause) 
                .Subscribe(When);
        }

        private async Task Update(IStream stream)
        {
            if(!_streams.TryGetValue(stream.Key, out var version))
                throw new InvalidOperationException("Stream not registered");
            
            _logger.Trace($"{stream.Key}@{stream.Version}",this);     
            
            var streamEvents = (await _eventStore.ReadStream(stream, version+1)).ToList();
            
            //if(!_streams.TryUpdate(stream.Key, version + streamEvents.Count, version))
            //    throw new InvalidOperationException("Stream version has already been modified!");
            //_logger.Debug($"{stream.Key}:{_streams[stream.Key]}");

            foreach (var e in streamEvents)
                When(e);
        }

        private void When(IEvent e)
        {
            if (e == null)
                return;

            if (!_handlers.TryGetValue(e.GetType(), out var handler)) 
                return;
            
            handler(e);
            _streams[e.Stream] = e.Version;
        }
    }
}