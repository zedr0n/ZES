using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodaTime;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces.Branching
{
    /// <summary>
    /// Timeline manager
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
        Task Ready { get; }

        /// <summary>
        /// Branches the current timeline at a specified point in the past.
        /// </summary>
        /// <param name="branchId">The unique identifier of the branch to create.</param>
        /// <param name="time">The timestamp to branch at; defaults to the current timeline's current time if not provided.</param>
        /// <param name="keys">The streams to include in the branch; null will include all streams.</param>
        /// <param name="deleteExisting">Indicates whether to delete an existing branch with the same identifier.</param>
        /// <param name="useLazy">Specifies whether the branch creation should be deferred (lazy) or immediate.</param>
        /// <returns>A task that represents the asynchronous operation, containing the newly created timeline.</returns>
        Task<ITimeline> Branch(string branchId, Time time = default, IEnumerable<string> keys = null, bool deleteExisting = false, bool? useLazy = null);

        /// <summary>
        /// Merges the timeline into active timeline
        /// </summary>
        /// <param name="branchId">Branch to merge</param>
        /// <param name="includeNewStreams">Whether to create new streams which don't exist on current branch</param>
        /// <returns>Completes when merge is completed</returns>
        Task<MergeResult> Merge(string branchId, bool includeNewStreams = true);

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

        /// <summary>
        /// Gets the current time of the branch
        /// </summary>
        /// <param name="branchId">Branch identifier</param>
        /// <returns>Current time</returns>
        Time GetTime(string branchId);
    }
    
    /// <summary>
    /// Merge result record 
    /// </summary>
    /// <param name="Success">Merge status</param>
    /// <param name="Changes">Merge changes</param>
    /// <param name="CommandChanges">Commmands merge changes</param>
    public readonly record struct MergeResult(bool Success, Dictionary<IStream, int> Changes, IEnumerable<ICommand> CommandChanges)
    {
    }
}