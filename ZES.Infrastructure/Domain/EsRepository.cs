// #define USE_ES_CACHE

using System;
using System.Collections.Concurrent;
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

#pragma warning disable CS4014

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class EsRepository<TEventSourced> : IEsRepository<TEventSourced>
        where TEventSourced : IEventSourced
    {
        private readonly IEventStore<TEventSourced> _eventStore;
        private readonly IStreamLocator _streams;
        private readonly ITimeline _timeline;
        private readonly IBus _bus;
        private readonly IMessageQueue _messageQueue;
        private readonly IEsRegistry _registry;

        private readonly ConcurrentDictionary<string, TEventSourced> _cache = new ConcurrentDictionary<string, TEventSourced>();
        
        private readonly ConcurrentDictionary<string, Func<string, Task<IEnumerable<IEvent>>>> _delegates = new ConcurrentDictionary<string, Func<string, Task<IEnumerable<IEvent>>>>(); 
        
        /// <summary>
        /// Initializes a new instance of the <see cref="EsRepository{I}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="streams">Stream locator</param>
        /// <param name="timeline">Active timeline tracker</param>
        /// <param name="bus">Message bus</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="registry">Event sourced registry</param>
        public EsRepository(IEventStore<TEventSourced> eventStore, IStreamLocator streams, ITimeline timeline, IBus bus, IMessageQueue messageQueue, IEsRegistry registry)
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
            where T : class, TEventSourced
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
                e.Timeline = _timeline.Id;
            }

            await _eventStore.AppendToStream(stream, events);
            if (es is ISaga saga)
            {
                var commands = saga.GetUncommittedCommands();
                foreach (var command in commands)
                    await _bus.CommandAsync(command);
            }
#if USE_ES_CACHE
            _cache[stream.Key] = es;
#endif
        }

        /// <inheritdoc />
        public async Task<T> GetOrAdd<T>(string id)
            where T : class, TEventSourced, new()
        {
            var instance = await Find<T>(id);
            return instance ?? EventSourced.Create<T>(id, 0);
        }

        /// <inheritdoc />
        public async Task<T> Find<T>(string id, bool computeHash = false)
            where T : class, TEventSourced, new()
        {
            var stream = _streams.Find<T>(id, _timeline.Id);
            if (stream == null)
                return null;
            TEventSourced es;
#if USE_ES_CACHE
            if (_cache.TryGetValue(stream.Key, out es) && es.Version == stream.Version)
            {
                es.Clear();
                return es as T;
            }
#endif

            var start = stream.SnapshotVersion;
            var events = await _eventStore.ReadStream<IEvent>(stream, start).ToList();
            if (events.Count == 0)
                return null;
            es = EventSourced.Create<T>(id, start - 1);
            es.LoadFrom<T>(events, computeHash);

            return es as T;
        }

        /// <inheritdoc />
        public async Task<bool> IsValid<T>(string id)
            where T : class, TEventSourced, new()
        {
            var stream = _streams.Find<T>(id, _timeline.Id);
            if (stream == null)
                return true;

            var start = stream.SnapshotVersion;
            var events = await _eventStore.ReadStream<IEvent>(stream, start).ToList();
            var es = EventSourced.Create<T>(id, start - 1);
            es.LoadFrom<T>(events, true);

            return es.IsValid;
        }

        /// <inheritdoc />
        public async Task<int> LastValidVersion<T>(string id) 
            where T : class, TEventSourced, new()
        {
            var stream = _streams.Find<T>(id, _timeline.Id);
            if (stream == null)
                return ExpectedVersion.NoStream;

            var start = stream.SnapshotVersion;            
            var events = await _eventStore.ReadStream<IEvent>(stream, start).ToList();
            var es = EventSourced.Create<T>(id, start - 1);
            es.LoadFrom<T>(events, true);

            return es.LastValidVersion;
        }
        
        /// <inheritdoc />
        public async Task<IEnumerable<IEvent>> FindInvalidEvents<T>(string id) 
            where T : class, TEventSourced, new()
        {
            var stream = _streams.Find<T>(id, _timeline.Id);
            if (stream == null)
                return null;
            
            // return new List<IEvent>();
            var start = stream.SnapshotVersion;            
            var events = await _eventStore.ReadStream<IEvent>(stream, start).ToList();
            var es = EventSourced.Create<T>(id, start - 1);
            es.LoadFrom<T>(events, true);

            return es.GetInvalidEvents();
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IEvent>> FindInvalidEvents(string type, string id)
        {
            var @delegate = _delegates.GetOrAdd(type, s =>
            {
                var t = _registry.GetType(s);
                var m = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .SingleOrDefault(x => x.IsGenericMethod && x.Name == nameof(FindInvalidEvents))
                    ?.MakeGenericMethod(t);

                return (Func<string, Task<IEnumerable<IEvent>>>)Delegate.CreateDelegate(typeof(Func<string, Task<IEnumerable<IEvent>>>), this, m);
            });
            
            return await @delegate(id);
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