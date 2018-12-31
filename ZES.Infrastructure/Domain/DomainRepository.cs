using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Domain
{
    public class DomainRepository : IDomainRepository
    {
        private readonly IEventStore _eventStore;
        private readonly IStreamLocator _streams;
        private readonly ITimeline _timeline;
        private readonly IBus _bus;
        
        public DomainRepository(IEventStore eventStore, IStreamLocator streams, ITimeline timeline, IBus bus)
        {
            _eventStore = eventStore;
            _streams = streams;
            _timeline = timeline;
            _bus = bus;
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
            if (es is ISaga saga)
            {
                var commands = saga.GetUncommittedCommands();
                foreach (var command in commands)
                    await _bus.CommandAsync(command);
            }
                
        }

        public async Task<T> GetOrAdd<T>(string id) where T : class, IEventSourced, new()
        {
            var instance = await Find<T>(id) ?? new T();
            var pastEvents = new List<IEvent>();
            instance.LoadFrom<T>(id, pastEvents);
            return instance;
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