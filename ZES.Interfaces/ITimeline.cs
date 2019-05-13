namespace ZES.Interfaces
{
    /// <summary>
    /// Timeline tracker 
    /// </summary>
    public interface ITimeline
    {
        /// <summary>
        /// Gets id of the alternate timeline we are in at the moment
        /// </summary>
        /// <value>
        /// Id of the alternate timeline we are in at the moment
        /// </value>
        string Id { get; }

        /// <summary>
        /// Gets current time in the timeline
        /// </summary>
        /// <value>
        /// Current time in the timeline
        /// </value>
        long Now { get; }

        /// <summary>
        /// Gets a value indicating whether whether it's a fixed timeline
        /// </summary>
        /// <value>
        /// A value indicating whether whether it's a fixed timeline
        /// </value>
        bool Live { get; }

        /// <summary>
        /// Sets the timeline to be a copy of target timeline
        /// </summary>
        /// <param name="rhs">target timeline</param>
        void Set(ITimeline rhs);
    }
}