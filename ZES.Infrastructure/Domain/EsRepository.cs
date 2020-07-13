using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

#pragma warning disable CS4014

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class EsRepository<I> : IEsRepository<I>
        where I : IEventSourced
    {
        private readonly IEventStore<I> _eventStore;
        private readonly IStreamLocator _streams;
        private readonly ITimeline _timeline;
        private readonly IBus _bus;
        private readonly IMessageQueue _messageQueue;
        private readonly IEsRegistry _registry;

        /// <summary>
        /// Initializes a new instance of the <see cref="EsRepository{I}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="streams">Stream locator</param>
        /// <param name="timeline">Active timeline tracker</param>
        /// <param name="bus">Message bus</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="registry">Event sourced registry</param>
        public EsRepository(IEventStore<I> eventStore, IStreamLocator streams, ITimeline timeline, IBus bus, IMessageQueue messageQueue, IEsRegistry registry)
        {
            _eventStore = eventStore;
            _streams = streams;
            _timeline = timeline;
            _bus = bus;
            _messageQueue = messageQueue;
            _registry = registry;
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

            var stream = _streams.GetOrAdd(es, _timeline.Id);
            if (stream.Version >= 0 && es.Version - events.Count < stream.Version)
                throw new InvalidOperationException($"Stream ( {stream.Key}@{stream.Version} ) is ahead of aggregate root ( {es.Version - events.Count} )");

            foreach (var e in events.Cast<Event>())
            {
                if (e.Timestamp == default(long))
                    e.Timestamp = _timeline.Now;
                e.Stream = stream.Key;
            }

            await _eventStore.AppendToStream(stream, events);
            if (es is ISaga saga)
            {
                var commands = saga.GetUncommittedCommands();
                foreach (var command in commands.Cast<Command>())
                {
                    await _messageQueue.UncompleteMessage(command);
                    var task = await _bus.CommandAsync(command);
                    task.ContinueWith(async t => await _messageQueue.CompleteMessage(command));
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
            var es = EventSourced.Create<T>(id);
            es.LoadFrom<T>(events);

            return es;
        }

        /// <inheritdoc />
        public async Task<bool> IsValid<T>(string id)
            where T : class, I, new()
        {
            var stream = _streams.Find<T>(id, _timeline.Id);
            if (stream == null)
                return true;

            var events = await _eventStore.ReadStream<IEvent>(stream, 0).ToList();
            var es = EventSourced.Create<T>(id);
            es.LoadFrom<T>(events, true);

            return es.IsValid;
        }

        /// <inheritdoc />
        public async Task<int> LastValidVersion<T>(string id) 
            where T : class, I, new()
        {
            var stream = _streams.Find<T>(id, _timeline.Id);
            if (stream == null)
                return ExpectedVersion.NoStream;
            
            var events = await _eventStore.ReadStream<IEvent>(stream, 0).ToList();
            var es = EventSourced.Create<T>(id);
            es.LoadFrom<T>(events, true);

            return es.LastValidVersion;
        }
        
        /// <inheritdoc />
        public async Task<IEnumerable<IEvent>> FindInvalidEvents<T>(string id) 
            where T : class, I, new()
        {
            var stream = _streams.Find<T>(id, _timeline.Id);
            if (stream == null)
                return null;
            
            // return new List<IEvent>();
            var events = await _eventStore.ReadStream<IEvent>(stream, 0).ToList();
            var es = EventSourced.Create<T>(id);
            es.LoadFrom<T>(events, true);

            return es.GetInvalidEvents();
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IEvent>> FindInvalidEvents(string type, string id)
        {
            var t = _registry.GetType(type);
            var m = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(x => x.IsGenericMethod && x.Name == nameof(FindInvalidEvents))
                ?.MakeGenericMethod(t);

            if (m != null)
            {
                var task = (Task<IEnumerable<IEvent>>)m.Invoke(this, new object[] { id });
                await task;
                return task.Result;
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<bool> IsValid(string type, string id)
        {
            var t = _registry.GetType(type);
            var m = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(x => x.IsGenericMethod && x.Name == nameof(IsValid))
                ?.MakeGenericMethod(t);

            if (m != null)
            {
                var task = (Task<bool>)m.Invoke(this, new object[] { id });
                await task;
                return task.Result;
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<int> LastValidVersion(string type, string id)
        {
            var t = _registry.GetType(type);
            var m = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(x => x.IsGenericMethod && x.Name == nameof(LastValidVersion))
                ?.MakeGenericMethod(t);

            if (m != null)
            {
                var task = (Task<int>)m.Invoke(this, new object[] { id });
                await task;
                return task.Result;
            }

            throw new NotImplementedException();
        }
    }
}