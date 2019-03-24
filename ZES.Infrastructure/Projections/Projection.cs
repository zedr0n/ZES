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
        private readonly ActionBlock<IEnumerable<IStream>> _updater;
        private readonly IEventStore _eventStore;
        private readonly ConcurrentDictionary<string, int> _streams = new ConcurrentDictionary<string, int>();
        private readonly Dictionary<Type, Action<IEvent>> _handlers = new Dictionary<Type, Action<IEvent>>();

        protected Projection(IEventStore eventStore)
        {
            _eventStore = eventStore;
            _updater = new ActionBlock<IEnumerable<IStream>>(Update,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1
                });
        }

        protected void Register<TEvent>(Action<TEvent> when) where TEvent : class
        {
            _handlers.Add(typeof(TEvent), e => when(e as TEvent)); 
        }

        protected Task Notify(IEnumerable<IStream> streams)
        {
            var updatedStreams = new List<IStream>();
            foreach (var stream in streams)
            {
                var version = stream.Version; 
                var projectionVersion = _streams.GetOrAdd(stream.Key,ExpectedVersion.EmptyStream);
                if (version > projectionVersion)
                    updatedStreams.Add(stream);
            }
            return _updater.SendAsync(updatedStreams);
        }

        private async Task Update(IEnumerable<IStream> streams)
        {
            var events = new List<IEvent>();
            foreach (var stream in streams)
            {
                var version = _streams[stream.Key];
                var streamEvents = (await _eventStore.ReadStream(stream, version+1)).ToList();
                
                events.AddRange(streamEvents); 
                _streams[stream.Key] = version + streamEvents.Count;    
            }

            foreach (var e in events.OrderBy(e => e.Timestamp))
                When(e);
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