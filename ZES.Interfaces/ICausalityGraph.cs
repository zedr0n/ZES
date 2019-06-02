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
    }
}