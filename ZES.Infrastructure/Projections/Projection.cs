using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    public class Projection
    {
        private readonly IEventStore _eventStore;
        private readonly ConcurrentDictionary<string, int> _streams = new ConcurrentDictionary<string, int>();
        private readonly Dictionary<Type, Action<IEvent>> _handlers = new Dictionary<Type, Action<IEvent>>();

        protected Projection(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        protected void Register<TEvent>(Action<TEvent> when) where TEvent : class
        {
            _handlers.Add(typeof(TEvent), e => when(e as TEvent)); 
        }
        
        protected async Task Update(IEnumerable<IStream> streams)
        {
            var events = new List<IEvent>();
            foreach (var stream in streams)
            {
                var version = _streams.GetOrAdd(stream.Key,0);
                var streamEvents = (await _eventStore.ReadStream(stream, version)).ToList();
                
                events.AddRange(streamEvents); 
                _streams[stream.Key] = version + streamEvents.Count;    
            }

            foreach (var e in events.OrderBy(e => e.Timestamp))
                When(e);
        }

        protected void When(IEvent e)
        {
            if (e == null)
                return;
            
            if (_handlers.TryGetValue(e.GetType(), out var handler))
                handler(e);
        }
    }
}