using NodaTime;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Query
    /// </summary>
    public interface IQuery
    {
        /// <summary>
        /// Gets or sets the timeline to query
        /// </summary>
        string Timeline { get; set; }
        
        /// <summary>
        /// Gets or sets the query timestamp
        /// </summary>
        Time Timestamp { get; set; }
    }

    /// <summary>
    /// CQRS Query
    /// </summary>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IQuery<TResult> : IQuery
    {
    }
    
    /// <summary>
    /// Historical derivative of underlying query
    /// </summary>
    /// <typeparam name="TQuery">Underlying query type</typeparam>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IHistoricalQuery<out TQuery, TResult> : IQuery<TResult>
    {
        /// <summary>
        /// Gets underlying query
        /// </summary>
        /// <value>
        /// Underlying query
        /// </value>
        TQuery Query { get; }
    }
}