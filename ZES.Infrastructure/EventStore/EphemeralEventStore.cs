using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.EventStore
{
    /// <summary>
    /// Event store decorator that routes ephemeral events to in-memory storage
    /// while persisting non-ephemeral events to the underlying store.
    /// Provides merged read access to both persistent and ephemeral events.
    /// </summary>
    /// <typeparam name="TEventSourced">Event-sourced types</typeparam>
    public class EphemeralEventStore<TEventSourced> : IEventStore<TEventSourced>
        where TEventSourced : IEventSourced
    {
        private readonly IEventStore<TEventSourced> _persistentStore;

        // In-memory storage for ephemeral events, keyed by stream ID
        private readonly ConcurrentDictionary<string, List<IEvent>> _ephemeralEvents = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EphemeralEventStore{TEventSourced}"/> class.
        /// </summary>
        /// <param name="persistentStore">The underlying persistent event store</param>
        public EphemeralEventStore(IEventStore<TEventSourced> persistentStore)
        {
            _persistentStore = persistentStore;
        }

        /// <inheritdoc />
        public IObservable<IStream> Streams => _persistentStore.Streams;

        /// <inheritdoc />
        public Task ResetDatabase() => _persistentStore.ResetDatabase();

        /// <inheritdoc />
        public IObservable<T> ReadStream<T>(IStream stream, int start, int count = -1, SerializationType serializationType = SerializationType.PayloadAndMetadata)
            where T : class, IEvent
        {
            var persistentEvents = _persistentStore.ReadStream<T>(stream, start, count, serializationType);

            // Get ephemeral events for this stream
            var ephemeralEvents = GetEphemeralEventsForStream<T>(stream.Key);

            if (ephemeralEvents.Count == 0)
                return persistentEvents;

            // Append ephemeral events at the end (they don't have versions since they're not stored)
            return persistentEvents.Concat(ephemeralEvents.ToObservable());
        }

        /// <inheritdoc />
        public Task<Time> GetTimestamp(IStream stream, int version) =>
            _persistentStore.GetTimestamp(stream, version);

        /// <inheritdoc />
        public Task<int> GetVersion(IStream stream, Time timestamp) =>
            _persistentStore.GetVersion(stream, timestamp);

        /// <inheritdoc />
        public Task<string> GetHash(IStream stream, int version = int.MaxValue) =>
            _persistentStore.GetHash(stream, version);

        /// <inheritdoc />
        public async Task AppendToStream(IStream stream, IEnumerable<IEvent> events = null, bool publish = true)
        {
            var eventList = events?.ToList();
            if (eventList == null || eventList.Count == 0)
            {
                await _persistentStore.AppendToStream(stream, eventList, publish);
                return;
            }

            // Separate ephemeral and persistent events
            var eventGroups = eventList.ToLookup(e => e.Ephemeral);
            var ephemeralEvents = eventGroups[true].ToList();
            var persistentEvents = eventGroups[false].ToList();
            if(persistentEvents.Count == 0)
                persistentEvents = null;
            
            // Store ephemeral events in memory
            if (ephemeralEvents.Count != 0)
            {
                var streamEvents = _ephemeralEvents.GetOrAdd(stream.Key, _ => []);
                lock (streamEvents)
                    streamEvents.AddRange(ephemeralEvents);
            }

            // Persist non-ephemeral events to underlying store
            await _persistentStore.AppendToStream(stream, persistentEvents, publish);
        }

        /// <inheritdoc />
        public Task DeleteStream(IStream stream) => _persistentStore.DeleteStream(stream);

        /// <inheritdoc />
        public Task TrimStream(IStream stream, int version) =>
            _persistentStore.TrimStream(stream, version);

        /// <inheritdoc />
        public IObservable<IStream> ListStreams(string branch = null, Func<string, bool> predicate = null, CancellationToken token = default) =>
            _persistentStore.ListStreams(branch, predicate, token);

        /// <summary>
        /// Clears all ephemeral events from memory
        /// </summary>
        public void ClearEphemeralEvents()
        {
            _ephemeralEvents.Clear();
        }

        /// <summary>
        /// Clears ephemeral events for a specific stream
        /// </summary>
        /// <param name="streamKey">The stream key</param>
        public void ClearEphemeralEvents(string streamKey)
        {
            _ephemeralEvents.TryRemove(streamKey, out _);
        }

        /// <inheritdoc />
        public bool HasEphemepheralEvents(IStream stream)
        {
            return _ephemeralEvents.TryGetValue(stream.Key, out var events) && events.Count > 0;
        }

        private List<T> GetEphemeralEventsForStream<T>(string streamKey)
            where T : class, IEvent
        {
            if (!_ephemeralEvents.TryGetValue(streamKey, out var events))
                return [];

            lock (events)
                return events.OfType<T>().OrderBy(e => e.Timestamp).ToList();
        }
    }
}
