using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
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

#pragma warning disable CS1998

namespace ZES.Persistence.EventStoreDB
{
    /// <inheritdoc />
    public class TcpEventStore<TEventSourced> : EventStoreBase<TEventSourced, EventData, RecordedEvent>
        where TEventSourced : IEventSourced
    {
        private readonly IEventStoreConnection _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpEventStore{TEventSourced}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">Serializer</param>
        /// <param name="log">Log service</param>
        /// <param name="connection">Event Store connection</param>
        public TcpEventStore(IMessageQueue messageQueue, ISerializer<IEvent> serializer, ILog log, IEventStoreConnection connection) 
            : base(messageQueue, serializer, log)
        {
            _connection = connection;
        }

        /// <inheritdoc />
        protected override Task ListStreamsObservable(IObserver<IStream> observer, Func<string, bool> predicate, CancellationToken token)
        {
            if (Configuration.UseEmbeddedTcpStore)
            {
                observer.OnCompleted();
                return Task.CompletedTask;
            }
            
            var streams = new HashSet<string>();
            var taskCompletionSource = new TaskCompletionSource<bool>();
            _connection.SubscribeToAllFrom(
                Position.Start,
                CatchUpSubscriptionSettings.Default,
                (sub, e) =>
                {
                    var streamId = e.Event?.EventStreamId;
                    if (!predicate(streamId) || !streams.Add(streamId)) 
                        return;

                    var stream = _connection.GetStream(streamId, Serializer).Result;
                    if (typeof(TEventSourced) == typeof(ISaga) && !stream.IsSaga) 
                        return;
                    if (typeof(TEventSourced) == typeof(IAggregate) && stream.IsSaga)
                        return;

                    observer.OnNext(stream);
                },
                sub =>
                {
                    sub.Stop();
                    observer.OnCompleted();
                    taskCompletionSource.SetResult(true);
                },
                (sub, reason, e) =>
                {
                    Log.Error(e.Message);
                    sub.Stop();
                    observer.OnCompleted();
                }, new UserCredentials("admin", "changeit"));
            return taskCompletionSource.Task;
        }

        /// <inheritdoc />
        protected override async Task ReadSingleStreamStore<TEvent>(IObserver<TEvent> observer, IStream stream, int position, int count, SerializationType serializationType = SerializationType.PayloadAndMetadata)
        {
            StreamEventsSlice slice;
            do
            {
                slice = await _connection.ReadStreamEventsForwardAsync(stream.Key, position, Math.Min(Configuration.BatchSize, count), false);
                position = (int)slice.NextEventNumber;

                count = await DecodeEventsObservable(observer, slice.Events.Select(e => e.Event), count);
            }
            while (count > 0 && !slice.IsEndOfStream);
        }

        /// <inheritdoc />
        protected override async Task<int> AppendToStreamStore(IStream stream, IList<EventData> streamMessages, IList<IEvent> events = null)
        {
            var version = stream.Version;
            if (stream.Parent != null && stream.Parent.Version > ExpectedVersion.NoStream)
                version -= stream.Parent.Version + 1;
            if (version < 0)
                version = ExpectedVersion.Any; 
            version = GetExpectedVersion(version);

            var result = await _connection.AppendToStreamAsync(stream.Key, version, streamMessages);
            return (int)result.NextExpectedVersion;
        }

        /// <inheritdoc />
        protected override async Task TruncateStreamStore(IStream stream, int version)
        {
            await _connection.DeleteStreamAsync(stream.Key, version);
        }

        /// <inheritdoc />
        protected override async Task DeleteStreamStore(IStream stream)
        {
            await _connection.DeleteStreamAsync(stream.Key, ExpectedVersion.Any);
        }

        /// <inheritdoc />
        protected override string GetEventJson(EventData message) => Encoding.UTF8.GetString(message.Data);

        /// <inheritdoc />
        protected override EventData EventToStreamMessage(IEvent e)
        {
            var jsonData = e.Json;
            var jsonMetadata = e.MetadataJson;
            if(jsonData == null)
                Serializer.SerializeEventAndMetadata(e, out jsonData, out jsonMetadata);
            
            return new ( e.MessageId.Id,
               e.MessageType,
               true,
               Encoding.UTF8.GetBytes(jsonData),
               Encoding.UTF8.GetBytes(jsonMetadata));
        }

        /// <inheritdoc />
        protected override async Task<T> StreamMessageToEvent<T>(RecordedEvent streamMessage, SerializationType serializationType = SerializationType.PayloadAndMetadata)
        {
            var json = Encoding.UTF8.GetString(streamMessage.Data);
            return Serializer.Deserialize(json) as T;
        }

        /// <inheritdoc />
        protected override int GetExpectedVersion(int version)
        {
            return version switch
            {
                ExpectedVersion.Any => global::EventStore.ClientAPI.ExpectedVersion.Any,
                ExpectedVersion.NoStream => global::EventStore.ClientAPI.ExpectedVersion.NoStream,
                ExpectedVersion.EmptyStream => global::EventStore.ClientAPI.ExpectedVersion.NoStream,
                _ => version
            };
        }

        /// <inheritdoc />
        protected override async Task UpdateStreamMetadata(IStream stream)
        {
            var metaVersion = (await _connection.GetStreamMetadataAsync(stream.Key)).MetastreamVersion;
            await _connection.SetStreamMetadataAsync(
                stream.Key,
                metaVersion, 
                Encoding.UTF8.GetBytes(EncodeStreamMetadata(stream))); // JExtensions.JStreamMetadata(stream));
        }
    }
}