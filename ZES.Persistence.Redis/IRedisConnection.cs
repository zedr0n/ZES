using StackExchange.Redis;

namespace ZES.Persistence.Redis
{
    /// <summary>
    /// Redis connection facade
    /// </summary>
    public interface IRedisConnection
    {
        /// <summary>
        /// Gets the redis connection multiplexer
        /// </summary>
        IConnectionMultiplexer Connection { get; }
        
        /// <summary>
        /// Gets or sets the database index
        /// </summary>
        int Database { get; set; }
    }
}