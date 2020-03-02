using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces
{
    /// <summary>
    /// Timeline manager
    /// ( currently supports clone ) 
    /// </summary>
    public interface IBranchManager
    {
        /// <summary>
        /// Gets active branch id
        /// </summary>
        /// <value>
        /// Active branch id
        /// </value>
        string ActiveBranch { get; }

        /// <summary>
        /// Gets observable which completes when there all activity has been completed
        /// </summary>
        IObservable<int> Ready { get; }

        /// <summary>
        /// Branch the current timeline at a certain point in the past
        /// </summary>
        /// <param name="branchId">Branch unique identifier</param>
        /// <param name="time">Timestamp to branch at</param>
        /// <param name="keys">Specific streams to branch</param>
        /// <returns>Task representing the newly branched timeline</returns>
        Task<ITimeline> Branch(string branchId, long? time = null, IEnumerable<string> keys = null);

        /// <summary>
        /// Merges the timeline into master
        /// </summary>
        /// <param name="branchId">Branch to merge</param>
        /// <returns>Master timeline</returns>
        Task Merge(string branchId);

        /// <summary>
        /// Reset the timeline
        /// </summary>
        /// <returns>Main timeline</returns>
        ITimeline Reset();

        /// <summary>
        /// Remove branch completely
        /// </summary>
        /// <param name="branchId">Branch id</param>
        /// <returns>Task completes when branch is deleted</returns>
        Task DeleteBranch(string branchId);

        /// <summary>
        /// Get changes from branch <paramref name="branchId"/> to current branch
        /// </summary>
        /// <param name="branchId">Branch with changes</param>
        /// <returns>Set of changes</returns>
        Task<Dictionary<IStream, int>> GetChanges(string branchId);
    }
}