namespace ZES.Interfaces
{
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
        
        /// <summary>
        /// Alternate timeline
        /// </summary>
        /// <param name="now"></param>
        void Set(long now);
    }
}