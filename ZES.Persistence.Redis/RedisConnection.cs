using System;
using System.Linq;
using StackExchange.Redis;

namespace ZES.Persistence.Redis
{
    /// <inheritdoc />
    public class RedisConnection : IRedisConnection
    {
        private readonly IConnectionMultiplexer _connection;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RedisConnection"/> class.
        /// </summary>
        /// <param name="connection">Redis connection multiplexer</param>
        public RedisConnection(IConnectionMultiplexer connection)
        {
            _connection = connection;
        }

        /// <inheritdoc />
        public int Database { get; set; } = 0;

        /// <param name="db"></param>
        /// <inheritdoc />
        public IDatabase GetDatabase(int? db = null)
        {
            return _connection.GetDatabase(db ?? Database);
        }

        /// <inheritdoc />
        public IServer GetServer()
        {
            var endpoint = _connection.GetEndPoints().FirstOrDefault();
            if (endpoint == default)
                throw new InvalidOperationException("No Redis server found");

            var server = _connection.GetServer(endpoint);
            return server;
        }
    }
}