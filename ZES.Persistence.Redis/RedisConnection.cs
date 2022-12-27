using StackExchange.Redis;

namespace ZES.Persistence.Redis
{
    /// <inheritdoc />
    public class RedisConnection : IRedisConnection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RedisConnection"/> class.
        /// </summary>
        /// <param name="connection">Redis connection multiplexer</param>
        public RedisConnection(IConnectionMultiplexer connection)
        {
            Connection = connection;
        }

        /// <inheritdoc />
        public IConnectionMultiplexer Connection { get; }

        /// <inheritdoc />
        public int Database { get; set; } = 0;
    }
}