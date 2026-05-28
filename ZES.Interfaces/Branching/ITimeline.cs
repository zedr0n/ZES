using NodaTime;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces.Branching
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
        Time Now { get; }

        /// <summary>
        /// Gets a value indicating whether it's a fixed timeline
        /// </summary>
        /// <value>
        /// A value indicating whether it's a fixed timeline
        /// </value>
        bool Live { get; }

        /// <summary>
        /// Gets or sets the active timeline, representing the current timeline
        /// being operated on, or null if on the master timeline.
        /// </summary>
        /// <value>
        /// The active timeline instance.
        /// </value>
        public ITimeline ActiveTimeline { get; set; }

        /// <summary>
        /// Warp to time
        /// </summary>
        /// <param name="time">Time to warp to</param>
        void Warp(Time time);
        
        /// <summary>
        /// Advances the timeline by the specified period.
        /// </summary>
        /// <param name="period">The duration to advance the timeline by.</param>
        void Advance(Period period);

        /// <summary>
        /// Create new timeline 
        /// </summary>
        /// <param name="id">Timeline id</param>
        /// <param name="time">Null for live or time for fixed timeline</param>
        /// <returns>New timeline</returns>
        ITimeline New(string id, Time time = default);
    }
}