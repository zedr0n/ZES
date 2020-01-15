using System.Threading.Tasks;

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
        /// Branch the current timeline at a certain point in the past
        /// </summary>
        /// <param name="branchId">Branch unique identifier</param>
        /// <param name="time">Timestamp to branch at</param>
        /// <returns>Task representing the newly branched timeline</returns>
        Task<ITimeline> Branch(string branchId, long? time = null);

        /// <summary>
        /// Merges the timeline into master
        /// </summary>
        /// <param name="branchId">Branch to merge</param>
        /// <param name="force">Force the merge removing the future events</param>
        /// <returns>Master timeline</returns>
        Task Merge(string branchId, bool force = false);

        /// <summary>
        /// Reset the timeline
        /// </summary>
        /// <returns>Main timeline</returns>
        ITimeline Reset();

        Task DeleteBranch(string branchId);
    }
}