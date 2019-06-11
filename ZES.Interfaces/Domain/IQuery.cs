namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Query
    /// </summary>
    public interface IQuery { }
    
    /// <summary>
    /// CQRS Query
    /// </summary>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IQuery<TResult> : IQuery { }
    
    /// <summary>
    /// Historical derivative of underlying query
    /// </summary>
    /// <typeparam name="TQuery">Underlying query type</typeparam>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IHistoricalQuery<out TQuery, TResult> : IQuery<TResult>
    {
        /// <summary>
        /// Gets time of the historical query
        /// </summary>
        /// <value>
        /// Historical timestamp
        /// </value>
        long Timestamp { get; }

        /// <summary>
        /// Gets underlying query
        /// </summary>
        /// <value>
        /// Underlying query
        /// </value>
        TQuery Query { get; }
    }
}