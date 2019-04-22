using System.Threading.Tasks;

namespace ZES.Interfaces
{
    public interface ITimeTraveller
    {
        /// <summary>
        /// Branch the current timeline at a certain point in the past
        /// </summary>
        /// <param name="branchId">Branch unique identifier</param>
        /// <param name="time">Timestamp to branch at</param>
        /// <returns>Task representing the newly branched timeline</returns>
        Task<ITimeline> Branch(string branchId, long time);

        /// <summary>
        /// Reset the timeline
        /// </summary>
        /// <returns>Main timeline</returns>
        ITimeline Reset();
    }
}