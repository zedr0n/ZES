using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;
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

        private enum RedisMetadata 
        {
            MessageType,
            LocalId,
            OriginId,
            AncestorId,
            CommandId,
            CorrelationId,
            RetroactiveId,
            OriginatingStream,
            MessageId,
            Timestamp,
            Version,
            Timeline,
            StreamHash,
            ContentHash,
            Stream
        }

        /// <inheritdoc />
        public override async Task ResetDatabase()
        {
            var server = _connection.GetServer();
            await server.FlushAllDatabasesAsync();
        }

        /// <inheritdoc />
        protected override async Task ListStreamsObservable(IObserver<IStream> observer, Func<string, bool> predicate, CancellationToken token)
        {
            var server = _connection.GetServer();
            var db = _connection.GetDatabase();
            var keys = server.KeysAsync();
            await foreach (var key in server.KeysAsync().WithCancellation(token))
            {
                if (!predicate(key) || !key.ToString().StartsWith("$$"))
                    continue;

                var metadata = await db.StringGetAsync(key);
                var stream = Serializer.DecodeStreamMetadata(metadata); 
                if (stream == null)
                    stream = new Stream(key);

                var parent = stream.Parent;
                while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
                {
                    var parentMetadata = await db.StringGetAsync($"$${parent.Key}");
                    if (parentMetadata == RedisValue.Null)
                        break;

                    var grandParent = Serializer.DecodeStreamMetadata(parentMetadata)?.Parent; 
                
                    parent.Parent = grandParent;
                    parent = grandParent;
                }
                
                observer.OnNext(stream);
            }
        }

        /// <inheritdoc />
        protected override async Task ReadSingleStreamStore<TEvent>(IObserver<TEvent> observer, IStream stream, int position, int count, SerializationType serializationType = SerializationType.PayloadAndMetadata)
        {
            var db = _connection.GetDatabase();
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
        protected override async Task<int> AppendToStreamStore(IStream stream, IList<StreamEntry> streamMessages, IList<IEvent> events = null)
        {
            var version = stream.Version;
            if (stream.Parent != null && stream.Parent.Version > ExpectedVersion.NoStream)
                version -= stream.Parent.Version + 1;

            var db = _connection.GetDatabase();
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
            success = await tran.ExecuteAsync();
            switch (success)
            {
                case true:
                    await Task.WhenAll(tasks);
                    break;
                case false:
                    throw new InvalidOperationException($"Append to stream {stream.Key} failed!");
            }

            return appendVersion + streamMessages.Count;
        }

        /// <inheritdoc />
        protected override async Task UpdateStreamMetadata(IStream stream)
        {
            var db = _connection.GetDatabase();

            // var success = await db.HashSetAsync(stream.Key, "metadataJson", Serializer.EncodeStreamMetadata(stream));
            var success = await db.StringSetAsync($"$${stream.Key}", Serializer.EncodeStreamMetadata(stream));
            if (!success)
                throw new InvalidOperationException("Updating stream metadata failed!");
        }

        /// <inheritdoc />
        protected override async Task TruncateStreamStore(IStream stream, int version)
        {
            var db = _connection.GetDatabase();
            var minId = $"{1}-{version + 1}";
            var maxId = "+";

            var ids = await db.StreamRangeAsync(stream.Key, minId, maxId);
            await db.StreamDeleteAsync(stream.Key, ids.Select(s => s.Id).ToArray());
        }

        /// <inheritdoc />
        protected override async Task DeleteStreamStore(IStream stream)
        {
            var db = _connection.GetDatabase();
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
        protected override StreamEntry EventToStreamMessage(IEvent e)
        {
            var ignoredProperties = new string[]
            {
                nameof(IEventStaticMetadata.MessageType),
                nameof(IEventStaticMetadata.LocalId),
                nameof(IEventStaticMetadata.OriginId),
                nameof(IEventStaticMetadata.AncestorId),
                nameof(IEventStaticMetadata.CommandId),
                nameof(IEventStaticMetadata.CorrelationId),
                nameof(IEventStaticMetadata.RetroactiveId),
                nameof(IEventStaticMetadata.OriginatingStream),
                nameof(IEventMetadata.MessageId),
                nameof(IEventMetadata.Timestamp),
                nameof(IEventMetadata.Version),
                nameof(IEventMetadata.Timeline),
                nameof(IEventMetadata.StreamHash),
                nameof(IEventMetadata.ContentHash),
                nameof(IEventMetadata.Stream)
            };
            var jsonData = e.Json;
            if (jsonData == null)
                Serializer.SerializeEventAndMetadata(e, out jsonData, out _, ignoredProperties);
            
            var entries = new NameValueEntry[]
            {
                new ( nameof(IEventStaticMetadata.MessageType), e.StaticMetadata.MessageType),
                new ( nameof(IEventStaticMetadata.LocalId), e.StaticMetadata.LocalId.ToString()),
                new ( nameof(IEventStaticMetadata.OriginId), e.StaticMetadata.OriginId.ToString()),
                new (nameof(IEventStaticMetadata.AncestorId), e.StaticMetadata.AncestorId?.ToString() ?? ""),
                new (nameof(IEventStaticMetadata.CommandId), e.StaticMetadata.CommandId?.ToString() ?? ""),
                new (nameof(IEventStaticMetadata.CorrelationId), e.StaticMetadata.CorrelationId ?? ""),
                new (nameof(IEventStaticMetadata.RetroactiveId), e.StaticMetadata.RetroactiveId?.ToString() ?? ""),
                new (nameof(IEventStaticMetadata.OriginatingStream), e.StaticMetadata.OriginatingStream ?? ""),
                new (nameof(IEventMetadata.MessageId), e.Metadata.MessageId.ToString()),
                new ( nameof(IEventMetadata.Timestamp), e.Metadata.Timestamp.Serialise()),
                new ( nameof(IEventMetadata.Version), e.Metadata.Version), 
                new ( nameof(IEventMetadata.Timeline), e.Metadata.Timeline),
                new ( nameof(IEventMetadata.StreamHash), e.Metadata.StreamHash),
                new ( nameof(IEventMetadata.ContentHash), e.Metadata.ContentHash),
                new ( nameof(IEventMetadata.Stream), e.Metadata.Stream),
                new ("jsonData", jsonData),
            };
            return new StreamEntry(e.Metadata.Version, entries);
        }

        /// <inheritdoc />
        protected override async Task<T> StreamMessageToEvent<T>(StreamEntry streamMessage, SerializationType serializationType = SerializationType.PayloadAndMetadata)
        {
            var json = streamMessage.Values.SingleOrDefault(v => v.Name == "jsonData");
            if (json == default)
                return null;

            var e = Serializer.Deserialize(json.Value) as T;
            if (e == null)
                throw new InvalidOperationException("Cannot deserialize event!");
            
            for (var i = 0; i < streamMessage.Values.Length; ++i)
            {
                var val = streamMessage.Values[i].Value; 
                switch (i)
                {
                    case (int)RedisMetadata.MessageType:
                        e.StaticMetadata.MessageType = val;
                        break;
                    case (int)RedisMetadata.LocalId:
                        e.StaticMetadata.LocalId = EventId.Parse(val);
                        break;
                    case (int)RedisMetadata.OriginId:
                        e.StaticMetadata.OriginId = EventId.Parse(val);
                        break;
                    case (int)RedisMetadata.AncestorId:
                        if (val != string.Empty)
                            e.StaticMetadata.AncestorId = MessageId.Parse(val);
                        break;
                    case (int)RedisMetadata.CommandId:
                        if(val != string.Empty)
                            e.StaticMetadata.CommandId = MessageId.Parse(val);
                        break;
                    case (int)RedisMetadata.CorrelationId:
                        if (val != string.Empty)
                            e.StaticMetadata.CorrelationId = val;
                        break;
                    case (int)RedisMetadata.RetroactiveId:
                        if (val != string.Empty)
                            e.StaticMetadata.RetroactiveId = MessageId.Parse(val);
                        break;
                    case (int)RedisMetadata.OriginatingStream:
                        if (val != string.Empty)
                            e.StaticMetadata.OriginatingStream = val;
                        break;
                    case (int)RedisMetadata.MessageId: // MessageId
                        e.Metadata.MessageId = MessageId.Parse(val);
                        break;
                    case (int)RedisMetadata.Timestamp:
                        e.Metadata.Timestamp = Time.Parse(val);
                        break;
                    case (int)RedisMetadata.Version:
                        e.Metadata.Version = (int)val;
                        break;
                    case (int)RedisMetadata.Timeline:
                        e.Metadata.Timeline = val;
                        break;
                    case (int)RedisMetadata.StreamHash:
                        e.Metadata.StreamHash = val;
                        break;
                    case (int)RedisMetadata.ContentHash:
                        e.Metadata.ContentHash = val;
                        break;
                    case (int)RedisMetadata.Stream:
                        e.Metadata.Stream = val;
                        break;
                }
            }
                
            return e;
        }

        /// <inheritdoc />
        protected override int GetExpectedVersion(int version)
        {
            throw new NotImplementedException();
        }
    }
}