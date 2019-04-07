using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public class Projection<TState> : IProjection where TState : new()
    {
        private readonly ILog _logger;
        protected readonly ITimeline Timeline;

        private IDisposable _connection = Disposable.Empty;
        private IDisposable _subscription;
        private readonly BufferBlock<IStream> _bufferBlock;
        private readonly ActionBlock<IStream> _actionBlock;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ConcurrentDictionary<string,int> _streams = new ConcurrentDictionary<string, int>();
        internal readonly Dictionary<Type, Func<IEvent,TState,TState>> Handlers = new Dictionary<Type, Func<IEvent, TState, TState>>();
            
        private readonly Func<string, bool> _streamFilter = s => true;
        
        public TState State { get; protected set; }

        protected Projection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue, ITimeline timeline)
        {
            _eventStore = eventStore;
            _logger = logger;
            Timeline = timeline;
            _actionBlock = new ActionBlock<IStream>(Update,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                });
            _bufferBlock = new BufferBlock<IStream>();
            Rebuild();
            _eventStore.Streams.Subscribe(async s => await Notify(s));
            messageQueue.Alerts.OfType<InvalidateProjections>().Subscribe(s => Rebuild());
        }

        protected void Register<TEvent>(Func<TEvent,TState,TState> when) where TEvent : class
        {
            Handlers.Add(typeof(TEvent), (e,s) => when(e as TEvent,s)); 
        }

        protected void Register(Type tEvent, Func<IEvent,TState,TState> when)
        {
            Handlers.Add(tEvent, when);
        }

        private async Task Notify(IStream stream)
        {
            if (!_streamFilter(stream.Key))
                return;
            
            var version = stream.Version; 
            var projectionVersion = _streams.GetOrAdd(stream.Key,-1);
            _logger.Trace($"{stream.Key}@{projectionVersion}",this); 
            if (version > projectionVersion)
                await _bufferBlock.SendAsync(stream);
        }

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
        
        protected void Rebuild()
        {
            Pause();
            State = new TState();
            
            bool StreamFilter(string s) => _streamFilter(s) && s.StartsWith(Timeline.Id);
            
            _subscription?.Dispose();
            _subscription = _eventStore.Events.Where(e => StreamFilter(e.Stream))
                .Finally(Unpause) 
                .Subscribe(When);
        }

        private async Task Update(IStream stream)
        {
            if(!_streams.TryGetValue(stream.Key, out var version))
                throw new InvalidOperationException("Stream not registered");
            
            _logger.Trace($"{stream.Key}@{stream.Version}",this);     
            
            var streamEvents = (await _eventStore.ReadStream(stream, version+1)).ToList();
            
            foreach (var e in streamEvents)
                When(e);
        }

        private void When(IEvent e)
        {
            if (e == null)
                return;

            if (!Handlers.TryGetValue(e.GetType(), out var handler)) 
                return;

            lock (State)
            {
                State = handler(e,State);    
            }
            _streams[e.Stream] = e.Version;
        }
    }
}