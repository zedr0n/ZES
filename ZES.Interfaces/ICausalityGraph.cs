using System;
using System.Collections.Generic;

namespace ZES.Interfaces
{
    /// <summary>
    /// Causality graph
    /// </summary>
    public interface ICausalityGraph
    {
        /// <summary>
        /// Get upstream dependencies of the message
        /// </summary>
        /// <param name="messageId">Unique message identifier</param>
        /// <returns>List of dependencies</returns>
        IEnumerable<Guid> GetCauses(Guid messageId);
        
        /// <summary>
        /// Get downstream dependencies of the message
        /// </summary>
        /// <param name="messageId">Unique message identifier</param>
        /// <returns>List of dependencies</returns>
        IEnumerable<Guid> GetDependents(Guid messageId);

        /// <summary>
        /// Get upstream dependencies of the particular version of aggregate 
        /// </summary>
        /// <param name="aggregate">Aggregate instance</param>
        /// <param name="version">Target version</param>
        /// <param name="timeline">Timeline to search for</param>
        /// <returns>List of dependencies</returns>
        IEnumerable<Guid> GetCauses(IAggregate aggregate, int version, string timeline);
    }
}