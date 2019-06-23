using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class EsRepository<I> : IEsRepository<I>
        where I : IEventSourced
    {
        private readonly IEventStore<I> _eventStore;
        private readonly IStreamLocator<I> _streams;
        private readonly ITimeline _timeline;
        private readonly IBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="EsRepository{I}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="streams">Stream locator</param>
        /// <param name="timeline">Active timeline tracker</param>
        /// <param name="bus">Message bus</param>
        public EsRepository(IEventStore<I> eventStore, IStreamLocator<I> streams, ITimeline timeline, IBus bus)
        {
            _eventStore = eventStore;
            _streams = streams;
            _timeline = timeline;
            _bus = bus;
        }

        /// <inheritdoc />
        public async Task Save<T>(T es, Guid? ancestorId = null)
            where T : class, I
        {
            if (es == null)
                return;

            var events = es.GetUncommittedEvents().ToList();
            if (events.Count == 0)
                return;

            var stream = _streams.GetOrAdd(es, _timeline.Id);
            if (stream.Version >= 0 && es.Version - events.Count < stream.Version)
                throw new InvalidOperationException($"Stream ( {stream.Key}@{stream.Version} ) is ahead of aggregate root ( {es.Version - events.Count} )");

            foreach (var e in events.Cast<Event>())
            {
                if (e.Timestamp != default(long))
                    e.Timestamp = _timeline.Now;
                e.Stream = stream.Key;
                if (ancestorId == null) 
                    continue;
                e.AncestorId = ancestorId.Value;
            }

            await _eventStore.AppendToStream(stream, events);
            if (es is ISaga saga)
            {
                var commands = saga.GetUncommittedCommands();
                foreach (var command in commands.Cast<Command>())
                {
                    if (ancestorId != null)
                        command.AncestorId = ancestorId.Value;
                    await _bus.CommandAsync(command);
                }
            }
        }

        /// <inheritdoc />
        public async Task<T> GetOrAdd<T>(string id)
            where T : class, I, new()
        {
            var instance = await Find<T>(id);
            return instance ?? EventSourced.Create<T>(id);
        }

        /// <inheritdoc />
        public async Task<T> Find<T>(string id)
            where T : class, I, new()
        {
            var stream = _streams.Find<T>(id, _timeline.Id);
            if (stream == null)
                return null;

            var events = await _eventStore.ReadStream<IEvent>(stream, 0).ToList();
            var aggregate = EventSourced.Create<T>(id);
            aggregate.LoadFrom<T>(events);

            return aggregate;
        }
    }
}