using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using ZES.Infrastructure;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;

#pragma warning disable CS1998

namespace ZES.Persistence.Redis
{
    /// <summary>
    /// Redis event store implementation
    /// </summary>
    /// <typeparam name="TEventSourced">Event sourced type</typeparam>
    public class RedisEventStore<TEventSourced> : EventStoreBase<TEventSourced, StreamEntry, StreamEntry> 
        where TEventSourced : IEventSourced
    {
        private readonly IRedisConnection _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisEventStore{TEventSourced}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="log">Log service</param>
        /// <param name="connection">Redis connection</param>
        public RedisEventStore(IMessageQueue messageQueue, ISerializer<IEvent> serializer, ILog log, IRedisConnection connection) 
            : base(messageQueue, serializer, log)
        {
            _connection = connection;
        }

        /// <inheritdoc />
        public override async Task ResetDatabase()
        {
            var connection = _connection.Connection;
            var endpoint = connection.GetEndPoints().FirstOrDefault();
            if (endpoint == default)
                throw new InvalidOperationException("No Redis server found");

            var server = connection.GetServer(endpoint);
            await server.FlushAllDatabasesAsync();
        }

        /// <inheritdoc />
        protected override async Task ListStreamsObservable(IObserver<IStream> observer, Func<string, bool> predicate, CancellationToken token)
        {
            var connection = _connection.Connection;
            var endpoint = connection.GetEndPoints().FirstOrDefault();
            if (endpoint == default)
                throw new InvalidOperationException("No Redis server found");

            var server = connection.GetServer(endpoint);
            var db = connection.GetDatabase(_connection.Database);
            var keys = server.KeysAsync();
            await foreach (var key in server.KeysAsync())
            {
                if (!predicate(key) || !key.ToString().StartsWith("$$"))
                    continue;

                var metadata = await db.StringGetAsync(key);
                var stream = Serializer.DecodeStreamMetadata(metadata); 
                if (stream == null)
                    stream = new Stream(key);

                /* var count = await streamStore.DeletedCount(key);
                 stream.AddDeleted(count); */

                var parent = stream.Parent;
                while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
                {
                    var parentMetadata = await db.StringGetAsync($"$${parent.Key}");
                    if (parentMetadata == RedisValue.Null)
                        break;

                    /* count = await streamStore.DeletedCount(parent.Key);
                     parent.AddDeleted(count);*/

                    var grandParent = Serializer.DecodeStreamMetadata(parentMetadata)?.Parent; 
                
                    parent.Parent = grandParent;
                    parent = grandParent;
                }
                
                observer.OnNext(stream);
            }
        }

        /// <inheritdoc />
        protected override async Task ReadSingleStreamStore<TEvent>(IObserver<TEvent> observer, IStream stream, int position, int count)
        {
            var connection = _connection.Connection;
            var db = connection.GetDatabase(_connection.Database);
            var minId = $"{1}-{position}";
            var maxId = "+";

            do
            {
                var entries = (await db.StreamRangeAsync(stream.Key, minId, maxId, Math.Min(Configuration.BatchSize, count))).ToList();
                if (entries.Count == 0)
                    break;
                count = await DecodeEventsObservable(observer, entries, count);
                minId = entries.Last().Id;
            } 
            while (count > 0);
            
            observer.OnCompleted();
        }

        /// <inheritdoc />
        protected override async Task<int> AppendToStreamStore(IStream stream, IList<StreamEntry> streamMessages)
        {
            var version = stream.Version;
            if (stream.Parent != null && stream.Parent.Version > ExpectedVersion.NoStream)
                version -= stream.Parent.Version + 1;
            /* if (version < 0)
                version = ExpectedVersion.Any; */

            var connection = _connection.Connection;
            var db = connection.GetDatabase(_connection.Database);
            var success = true;
            
            // check version for optimistic concurrency
            var streamLength = await db.StreamLengthAsync(stream.Key);
            if ((version == ExpectedVersion.NoStream || version == ExpectedVersion.EmptyStream) && streamLength > 0)
                throw new InvalidOperationException($"Append to {stream.Key} failed, stream already exists!");
            if (version > 0 && streamLength != version + 1)
                throw new InvalidOperationException($"Append to {stream.Key} failed, expected version : {streamLength}");

            // update stream message ids
            var resolvedStreamMessages = streamMessages
                .Select(e => new StreamEntry($"{stream.DeletedCount + 1}-{e.Id}", e.Values));

            var appendVersion = version < 0 ? -1 : version;
            var tran = db.CreateTransaction();
            tran.AddCondition(Condition.StreamLengthEqual(stream.Key, appendVersion + 1));
            var tasks = resolvedStreamMessages.Select(e => tran.StreamAddAsync(stream.Key, e.Values, e.Id)).Cast<Task>().ToList();
            success = tran.Execute();
            if (success)
                await Task.WhenAll(tasks);

            if (!success)
                throw new InvalidOperationException($"Append to stream {stream.Key} failed!");
            return appendVersion + streamMessages.Count;
        }

        /// <inheritdoc />
        protected override async Task UpdateStreamMetadata(IStream stream)
        {
            var connection = _connection.Connection;
            var db = connection.GetDatabase(_connection.Database);

            // var success = await db.HashSetAsync(stream.Key, "metadataJson", Serializer.EncodeStreamMetadata(stream));
            var success = await db.StringSetAsync($"$${stream.Key}", Serializer.EncodeStreamMetadata(stream));
            if (!success)
                throw new InvalidOperationException("Updating stream metadata failed!");
        }

        /// <inheritdoc />
        protected override async Task TruncateStreamStore(IStream stream, int version)
        {
            var connection = _connection.Connection;
            var db = connection.GetDatabase(_connection.Database);
            var minId = $"{1}-{version + 1}";
            var maxId = "+";

            var ids = await db.StreamRangeAsync(stream.Key, minId, maxId);
            await db.StreamDeleteAsync(stream.Key, ids.Select(s => s.Id).ToArray());
        }

        /// <inheritdoc />
        protected override async Task DeleteStreamStore(IStream stream)
        {
            var connection = _connection.Connection;
            var db = connection.GetDatabase(_connection.Database);
            await db.KeyDeleteAsync(stream.Key);
            await db.KeyDeleteAsync($"$${stream.Key}");
        }

        /// <inheritdoc />
        protected override string GetEventJson(StreamEntry message)
        {
            var json = message.Values.SingleOrDefault(v => v.Name == "jsonData");
            if (json != default)
                return json.Value;
            return null;
        }

        /// <inheritdoc />
        protected override StreamEntry EventToStreamMessage(IEvent e, string jsonData, string jsonMetadata)
        {
            var entries = new NameValueEntry[]
            {
                new ("MessageId", e.MessageId.ToString()),
                new ("MessageType", e.MessageType),
                new ("jsonData", jsonData),
                new ("jsonMetadata", jsonMetadata),
            };
            return new StreamEntry(e.Version, entries);
        }

        /// <inheritdoc />
        protected override async Task<T> StreamMessageToEvent<T>(StreamEntry streamMessage)
        {
            if (typeof(T) == typeof(IEvent))
            {
                var json = streamMessage.Values.SingleOrDefault(v => v.Name == "jsonData");
                if (json != default)
                    return Serializer.Deserialize(json.Value) as T;
                return null;
            }

            if (typeof(T) == typeof(IEventMetadata))
            {
                var json = streamMessage.Values.SingleOrDefault(v => v.Name == "jsonMetadata");
                if (json != default)
                    return Serializer.DecodeMetadata(json.Value) as T;
                return null;
            }

            return null;
        }

        /// <inheritdoc />
        protected override int GetExpectedVersion(int version)
        {
            throw new NotImplementedException();
        }
    }
}