using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;

namespace ZES.Persistence.Redis
{
    /// <inheritdoc />
    public class RedisCommandLog : CommandLogBase<StreamEntry, StreamEntry>
    {
        private readonly IRedisConnection _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisCommandLog"/> class.
        /// </summary>
        /// <param name="serializer">Command serializer</param>
        /// <param name="timeline">Timeline instance</param>
        /// <param name="log">Log service</param>
        /// <param name="connection">Redis connection</param>
        public RedisCommandLog(ISerializer<ICommand> serializer, ITimeline timeline, ILog log, IRedisConnection connection) 
            : base(serializer, timeline, log)
        {
            _connection = connection;
        }

        /// <inheritdoc />
        public override async Task DeleteBranch(string branchId)
        {
            var server = _connection.GetServer();
            var db = _connection.GetDatabase();
            await foreach (var key in server.KeysAsync())
            {
                if (!key.ToString().StartsWith($"{branchId}:Command"))
                    continue;

                await db.KeyDeleteAsync(key);
            }
        }

        /// <inheritdoc />
        protected override async Task ReadStreamStore(IObserver<ICommand> observer, IStream stream, int position, int count)
        {
            var db = _connection.GetDatabase();
            var minId = $"1-{position}";
            var maxId = "+";

            do
            {
                var entries = (await db.StreamRangeAsync(stream.Key, minId, maxId, Math.Min(Configuration.BatchSize, count))).ToList();
                if (entries.Count == 0)
                    break;
                
                foreach (var s in entries)
                {
                    var jsonData = s.Values.SingleOrDefault(v => v.Name == "jsonData").Value;
                    var command = Serializer.Deserialize(jsonData);
                    observer.OnNext(command);
                    count--;
                    if (count <= 0)
                        break;
                }
                
                var ids = entries.Last().Id.ToString().Split('-').Select(long.Parse).ToList();
                minId = $"{ids[0]}-{++ids[1]}";
            } 
            while (count > 0);
            
            observer.OnCompleted();
        }

        /// <inheritdoc />
        protected override async Task<List<string>> ListStreamsStore()
        {
            var streams = new List<string>();
            var server = _connection.GetServer();
            await foreach (var key in server.KeysAsync(pattern: "*:Command:*"))
                streams.Add(key);

            return streams;
        }

        /// <inheritdoc />
        protected override async Task<int> AppendToStream(string key, StreamEntry message)
        {
            var db = _connection.GetDatabase();
            var id = await db.StreamAddAsync(key, message.Values);
            var version = await db.StreamLengthAsync(key);
            
            return (int)version;
        }

        /// <inheritdoc />
        protected override StreamEntry Encode(ICommand command)
        {
            var jsonData = Serializer.Serialize(command);
            var entries = new NameValueEntry[]
            {
                new ("MessageId", command.MessageId.ToString()),
                new ("MessageType", command.GetType().Name),
                new ("jsonData", jsonData),
            };
            return new StreamEntry("*", entries);
        }
    }
}