using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    public class Projection
    {
        private readonly BatchBlock<IStream> _batchBlock;
        private readonly IEventStore<IAggregate> _eventStore;
        //private readonly ConcurrentDictionary<string, int> _streamVersions = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string,IStream> _streams = new ConcurrentDictionary<string, IStream>();
        private readonly Dictionary<Type, Action<IEvent>> _handlers = new Dictionary<Type, Action<IEvent>>();

        private const int BatchSize = 100;
        private long _timestamp;
        
        protected Projection(IEventStore<IAggregate> eventStore)
        {
            _eventStore = eventStore;
            var actionBlock = new ActionBlock<IEnumerable<IStream>>(Update,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1
                });
            
            _batchBlock = new BatchBlock<IStream>(BatchSize);
            _batchBlock.LinkTo(actionBlock);

            _eventStore.StreamsBatched.Subscribe(b => _batchBlock.TriggerBatch());
        }

        protected void Register<TEvent>(Action<TEvent> when) where TEvent : class
        {
            _handlers.Add(typeof(TEvent), e => when(e as TEvent)); 
        }

        protected async Task Notify(IStream stream)
        {
            var version = stream.Version; 
            var projectionVersion = _streams.GetOrAdd(stream.Key,stream.Clone(-1)).Version;
            if (version > projectionVersion)
                await _batchBlock.SendAsync(stream);
        }

        private async Task Rebuild()
        {
            var streams = new List<IStream>(_streams.Values);
            _timestamp = 0;
            await Update(streams.Select(s => s.Clone(-1)));
        }

        private async Task Update(IEnumerable<IStream> streams)
        {
            var events = new List<IEvent>();
            foreach (var stream in streams)
            {
                if(!_streams.TryGetValue(stream.Key, out var myStream))
                    throw new InvalidOperationException("Stream not registered");
                
                var streamEvents = (await _eventStore.ReadStream(stream, myStream.Version+1)).ToList();
                
                events.AddRange(streamEvents);
                if(!_streams.TryUpdate(stream.Key, myStream.Clone(myStream.Version + streamEvents.Count), myStream))
                    throw new InvalidOperationException("Stream version has already been modified!");
                //_streams[stream.Key].Version = myStream.Version + streamEvents.Count;    
            }

            if (events.Select(e => e.Timestamp).Min() >= _timestamp)
            {
                _timestamp = events.Select(e => e.Timestamp).Max(); 
                foreach (var e in events.OrderBy(e => e.Timestamp))
                    When(e);
            }
            else
                await Rebuild();
        }

        private void When(IEvent e)
        {
            if (e == null)
                return;
            
            if (_handlers.TryGetValue(e.GetType(), out var handler))
                handler(e);
        }
    }
}