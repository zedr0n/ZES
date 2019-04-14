using System.Threading.Tasks;

namespace ZES.Interfaces
{

    public interface ITimeTraveller
    {
        /// <summary>
        /// Branch the current timeline at a certain point in the past
        /// </summary>
        Task<ITimeline> Branch(string branchId, long time);

        ITimeline Reset();
    }
    
    public interface ITimeline
    {
        /// <summary>
        /// id of the alternate timeline we are in at the moment
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Current timestamp in the timeline
        /// </summary>
        long Now { get; }
    }
}