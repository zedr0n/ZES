using StackExchange.Redis;

namespace ZES.Persistence.Redis
{
    /// <summary>
    /// Redis connection facade
    /// </summary>
    public interface IRedisConnection
    {
        /// <summary>
        /// Gets or sets the database index
        /// </summary>
        int Database { get; set; }

        /// <summary>
        /// Obtain an interactive connection to a database inside redis.
        /// </summary>
        /// <param name="db">Specific database to request</param>
        /// <returns>Interactive connection to a database inside redis</returns>
        IDatabase GetDatabase(int? db = null);
        
        /// <summary>
        /// Obtain a configuration API for an individual server.
        /// </summary>
        /// <returns>Server configuration api</returns>
        IServer GetServer();
    }
}