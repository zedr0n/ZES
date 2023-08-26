#define USE_CUSTOM_SQLSTREAMSTORE

using System;
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
        protected override async Task ReadSingleStreamStore<TEvent>(IObserver<TEvent> observer, IStream stream, int position, int count, bool deserialize = true)
        {
            var page = await _streamStore.ReadStreamForwards(stream.Key, position, Math.Min(Configuration.BatchSize, count));
            while (page.Messages.Length > 0 && count > 0)
            {
                count = await DecodeEventsObservable(observer, page.Messages, count, deserialize);
                page = await page.ReadNext();
            }
            
            observer.OnCompleted();
        }

        /// <inheritdoc/>
        protected override async Task<int> AppendToStreamStore(IStream stream, IList<NewStreamMessage> streamMessages)
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
            var events = await ReadStream<IEventMetadata>(stream, version + 1).ToList();
            foreach (var e in events.Reverse())
                await _streamStore.DeleteMessage(stream.Key, e.MessageId.Id);
        }

        /// <inheritdoc />
        protected override async Task DeleteStreamStore(IStream stream)
        {
            await _streamStore.DeleteStream(stream.Key);
        }

        /// <inheritdoc />
        protected override string GetEventJson(NewStreamMessage message) => message.JsonData;

        /// <inheritdoc />
        protected override NewStreamMessage EventToStreamMessage(IEvent e)
        {
            var jsonData = e.Json;
            var jsonMetadata = e.JsonMetadata;
            if(jsonData == null)
                Serializer.SerializeEventAndMetadata(e, out jsonData, out jsonMetadata);
            
            return new (e.MessageId.Id, e.MessageType, jsonData, jsonMetadata);
        }

        /// <inheritdoc />
        protected override async Task<T> StreamMessageToJson<T>(StreamMessage streamMessage)
        {
            if (typeof(T) == typeof(IEvent))
            {
                var json = await streamMessage.GetJsonData();
                var e = Serializer.Deserialize(json, false);
                e.Json = json;
                e.JsonMetadata = streamMessage.JsonMetadata;
                return e as T;
            }

            if (typeof(T) == typeof(IEventMetadata))
            {
                var json = streamMessage.JsonMetadata;
                return new EventMetadata { Json = json } as T;
            }

            return null;
        }

        /// <inheritdoc />
        protected override async Task<T> StreamMessageToEvent<T>(StreamMessage streamMessage, bool deserialize = true)
        {
            if (typeof(T) == typeof(IEvent))
            {
                var json = await streamMessage.GetJsonData();
                var e = Serializer.Deserialize(json, deserialize);
                //e.Version = await GetVersion()
                e.Json = json;
                e.JsonMetadata = streamMessage.JsonMetadata;
                return e as T;
            }

            if (typeof(T) == typeof(IEventMetadata))
            {
                var json = streamMessage.JsonMetadata;
                return Serializer.DecodeMetadata(json) as T;
            }

            return null;
        }

        /// <inheritdoc />
        protected override int GetExpectedVersion(int version) => version;
    }
}