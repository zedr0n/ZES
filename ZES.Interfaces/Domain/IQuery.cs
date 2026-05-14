using System.Collections.Generic;
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

        /// <summary>
        /// Gets or sets the collection of additional timestamps to include in the query.
        /// </summary>
        IReadOnlyList<Time> AdditionalTimestamps { get; set; }
    }

    /// <summary>
    /// CQRS Query
    /// </summary>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IQuery<TResult> : IQuery
    {
    }
}