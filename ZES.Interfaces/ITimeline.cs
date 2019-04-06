namespace ZES.Interfaces
{
    public interface ITimeline
    {
        // id of the alternate timeline we are in
        // at the moment
        // Empty if live
        string Id { get; }
        
        /// <summary>
        /// Current timestamp in the timeline
        /// </summary>
        /// <returns></returns>
        long Now { get; }
        
        /// <summary>
        /// Alternate timeline
        /// </summary>
        /// <param name="now"></param>
        void Set(long now);
    }
}