using System;
using System.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Domain
{
    public class DomainRepository : IDomainRepository
    {
        private readonly IEventStore _eventStore;
        private readonly IStreamLocator _streams;
        private readonly ITimeline _timeline;
        
        public DomainRepository(IEventStore eventStore, IStreamLocator streams, ITimeline timeline)
        {
            _eventStore = eventStore;
            _streams = streams;
            _timeline = timeline;
        }
        
        public async Task Save<T>(T es) where T : class, IEventSourced
        {
            if (es == null)
                return;

            var events = es.GetUncommittedEvents().ToList();
            if (events.Count == 0)
                return;
            
            var stream = _streams.GetOrAdd(es);
            if (stream.Version >= 0 && (await _eventStore.ReadStream(stream, stream.Version)).Any())
                //throw new ConcurrencyException(stream.Key);
                throw new InvalidOperationException();

            foreach (var @event in events)
            {
                @event.EventId = Guid.NewGuid();
                @event.Timestamp = _timeline.Now;
            }
                   
            await _eventStore.AppendToStream(stream, events);    
        }

        public async Task<T> Find<T>(string id) where T : class, IEventSourced, new()
        {
            var stream = _streams.Find<T>(id, _timeline.TimelineId);
            if (stream == null)
                return null;

            var events = await _eventStore.ReadStream(stream, 0);
            var aggregate = new T();
            aggregate.LoadFrom<T>(id, events);
            
            return aggregate;
        }
    }
}