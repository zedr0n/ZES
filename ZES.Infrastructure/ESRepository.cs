using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
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
        public async Task Save<T>(T es)
            where T : class, I
        {
            if (es == null)
                return;

            var events = es.GetUncommittedEvents().ToList();
            if (events.Count == 0)
                return;

            var stream = _streams.GetOrAdd(es);
            if (stream.Version >= 0 && es.Version - events.Count < stream.Version)
                throw new InvalidOperationException($"Stream ( {stream.Version} ) is ahead of aggregate root ( {es.Version - events.Count} )");

            foreach (var e in events.OfType<Event>())
            {
                e.Timestamp = _timeline.Now;
                e.Stream = stream.Key;
            }

            await _eventStore.AppendToStream(stream, events);
            if (es is ISaga saga)
            {
                var commands = saga.GetUncommittedCommands();
                foreach (var command in commands)
                    await _bus.CommandAsync(command);
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

            var events = await _eventStore.ReadStream(stream, 0).ToList();
            var aggregate = EventSourced.Create<T>(id);
            aggregate.LoadFrom<T>(events);

            return aggregate;
        }
    }
}