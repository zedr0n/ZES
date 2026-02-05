#define USE_CUSTOM_SQLSTREAMSTORE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;
using ExpectedVersion = ZES.Infrastructure.EventStore.ExpectedVersion;

namespace ZES.Persistence.SQLStreamStore
{
    /// <summary>
    /// SQLStreamStore event store facade
    /// </summary>
    /// <typeparam name="TEventSourced">Event sourced type</typeparam>
    public class SqlEventStore<TEventSourced> : EventStoreBase<TEventSourced, NewStreamMessage, StreamMessage>
        where TEventSourced : IEventSourced
    {
        private readonly IStreamStore _streamStore;
        private readonly ConcurrentDictionary<Guid, IEvent> _eventCache = new();

        // Fast-path cache for temporary branches - bypasses InMemoryStreamStore writes
        // Use ConcurrentQueue to preserve event order (critical for hash validation)
        private readonly ConcurrentDictionary<string, ConcurrentQueue<IEvent>> _tempBranchCache = new();
        private readonly ConcurrentDictionary<string, int> _tempBranchVersions = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlEventStore{TEventSourced}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">Event serializer</param>
        /// <param name="log">Log service</param>
        /// <param name="streamStore">Stream store</param>
        public SqlEventStore(IMessageQueue messageQueue, ISerializer<IEvent> serializer, ILog log, IStreamStore streamStore)
            : base(messageQueue, serializer, log)
        {
            _streamStore = streamStore;
        }

        /// <inheritdoc />
        protected override async Task ListStreamsObservable(IObserver<IStream> observer, Func<string, bool> predicate, CancellationToken token)
        {
            var page = await _streamStore.ListStreams(Pattern.Anything(), Configuration.BatchSize, default, token);
            while (page.StreamIds.Length > 0 && !token.IsCancellationRequested)
            {
                foreach (var s in page.StreamIds.Where(predicate))
                {
                    var stream = await _streamStore.GetStream(s, Serializer).Timeout();
                        
                    if (typeof(TEventSourced) == typeof(ISaga) && !stream.IsSaga) 
                        continue;
                    if (typeof(TEventSourced) == typeof(IAggregate) && stream.IsSaga)
                        continue;
                        
                    observer.OnNext(stream);
                }

                page = await page.Next(token);
            }

            observer.OnCompleted();
        }

        /// <inheritdoc />
        protected override async Task ReadSingleStreamStore<TEvent>(IObserver<TEvent> observer, IStream stream, int position, int count, SerializationType serializationType = SerializationType.PayloadAndMetadata)
        {
            // Fast-path for temporary branches: read from memory cache
            if (stream.IsTemporary && _tempBranchCache.TryGetValue(stream.Key, out var cachedEvents))
            {
                // Convert queue to array to get stable snapshot with proper indexing
                var eventsArray = cachedEvents.ToArray();
                var startIndex = position;
                var takeCount = count > 0 ? count : eventsArray.Length - startIndex;

                for (int i = startIndex; i < startIndex + takeCount && i < eventsArray.Length; i++)
                {
                    if (eventsArray[i] is TEvent typedEvent)
                        observer.OnNext(typedEvent);
                }
                observer.OnCompleted();
                return;
            }

            var page = await _streamStore.ReadStreamForwards(stream.Key, position, Math.Min(Configuration.BatchSize, count));
            while (page.Messages.Length > 0 && count > 0)
            {
                count = await DecodeEventsObservable(observer, page.Messages, count, serializationType);
                page = await page.ReadNext();
            }

            observer.OnCompleted();
        }

        /// <inheritdoc/>
        protected override async Task<int> AppendToStreamStore(IStream stream, IList<NewStreamMessage> streamMessages, IList<IEvent> events = null)
        {
            var version = stream.Version;
            version += stream.DeletedCount;
            if (stream.Parent != null && stream.Parent.Version > ExpectedVersion.NoStream)
            {
                version -= stream.Parent.Version + 1;
                if (version < 0)
                    version = ExpectedVersion.NoStream;
            }

            version = GetExpectedVersion(version);

            // Fast-path for temporary branches: skip InMemoryStreamStore writes, cache events in memory only
            if (stream.IsTemporary && events != null)
            {
                var queue = _tempBranchCache.GetOrAdd(stream.Key, _ => new ConcurrentQueue<IEvent>());
                foreach (var evt in events)
                    queue.Enqueue(evt);

                var newVersion = _tempBranchVersions.AddOrUpdate(
                    stream.Key,
                    events.Count - 1,
                    (_, old) => old + events.Count);

                return newVersion - stream.DeletedCount;
            }

            // Normal path: write to InMemoryStreamStore
            #if USE_CUSTOM_SQLSTREAMSTORE
            var result = await _streamStore.AppendToStream(stream.Key, version, streamMessages.ToArray(), default, false);
            #else
                var result = await _streamStore.AppendToStream(stream.Key, stream.AppendPosition(), streamMessages.ToArray());
            #endif
            return result.CurrentVersion - stream.DeletedCount;
        }

        /// <inheritdoc />
        protected override async Task UpdateStreamMetadata(IStream stream)
        {
            // Fast-path for temporary branches: skip metadata updates
            if (stream.IsTemporary)
                return;

            /*var metaVersion = (await _streamStore.GetStreamMetadata(stream.Key)).MetadataStreamVersion;
            if (metaVersion == ExpectedVersion.EmptyStream)
                metaVersion = ExpectedVersion.NoStream;*/
            var metaVersion = ExpectedVersion.Any;

            await _streamStore.SetStreamMetadata(
                stream.Key,
                metaVersion,
                metadataJson: Serializer.EncodeStreamMetadata(stream));
        }

        /// <inheritdoc />
        protected override async Task TruncateStreamStore(IStream stream, int version)
        {
            var events = await ReadStream<IEvent>(stream, version + 1, -1, SerializationType.Metadata).ToList();
            foreach (var e in events.Reverse())
                await _streamStore.DeleteMessage(stream.Key, e.MessageId.Id);
        }

        /// <inheritdoc />
        protected override async Task DeleteStreamStore(IStream stream)
        {
            // Clean up temp branch cache
            if (stream.IsTemporary)
            {
                _tempBranchCache.TryRemove(stream.Key, out _);
                _tempBranchVersions.TryRemove(stream.Key, out _);
            }

            await _streamStore.DeleteStream(stream.Key);
        }

        /// <inheritdoc />
        protected override string GetEventJson(NewStreamMessage message) => message.JsonData + message.JsonMetadata;

        /// <inheritdoc />
        protected override NewStreamMessage EventToStreamMessage(IEvent e)
        {
            var jsonData = e.Json;
            var jsonMetadata = e.MetadataJson;
            if (jsonData == null)
                Serializer.SerializeEventAndMetadata(e, out jsonData, out jsonMetadata);
            else if (jsonMetadata == null)
                jsonMetadata = Serializer.EncodeMetadata(e);
            if (e.LocalId != null && e.LocalId.ReplicaName == Configuration.ReplicaName)
            {
                e.Json = jsonData;
                e.MetadataJson = jsonMetadata;
                _eventCache[e.MessageId.Id] = e;
            }

            return new NewStreamMessage(e.MessageId.Id, e.GetType().Name, jsonData, jsonMetadata);
        }

        /// <inheritdoc />
        protected override async Task<T> StreamMessageToEvent<T>(StreamMessage streamMessage, SerializationType serializationType = SerializationType.PayloadAndMetadata)
        {
            var messageId = streamMessage.MessageId;
            if (_eventCache.TryGetValue(messageId, out var e))
                return e as T;

            var json = await streamMessage.GetJsonData();
            var jsonMetadata = streamMessage.JsonMetadata;
            
            e = Serializer.Deserialize(json, jsonMetadata, serializationType);
            _eventCache[messageId] = e;
            return e as T;
        }

        /// <inheritdoc />
        protected override int GetExpectedVersion(int version) => version;
    }
}