using System.Collections.Generic;
using NodaTime;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Event sourced instance
    /// </summary>
    public interface IEventSourced
    {
        /// <summary>
        /// Gets event sourced id 
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Gets a value indicating whether the aggregate is valid ( hash agrees with events )
        /// </summary>
        bool IsValid { get; }
        
        /// <summary>
        /// Gets last valid version
        /// </summary>
        int LastValidVersion { get; }

        /// <summary>
        /// Gets the latest update timestamp 
        /// </summary>
        Time Timestamp { get; }

        /// <summary>
        /// Gets event sourced version ( for optimistic concurrency )
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Gets events not yet committed 
        /// </summary>
        /// <returns>Sequence of events</returns>
        IEnumerable<IEvent> GetUncommittedEvents();
        
        /// <summary>
        /// Gets invalid events 
        /// </summary>
        /// <returns>Sequence of invalid events</returns>
        IEnumerable<IEvent> GetInvalidEvents();

        /// <summary>
        /// Clear the changes
        /// </summary>
        void Clear();
        
        /// <summary>
        /// Hydrate the event sourced instance from event sequence
        /// </summary>
        /// <param name="pastEvents">Past event sequence</param>
        /// <param name="computeHash">Compute the events hash</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        void LoadFrom<T>(IEnumerable<IEvent> pastEvents, bool computeHash = false)
            where T : class, IEventSourced;

        /// <summary>
        /// Timestamp the uncommitted events
        /// </summary>
        /// <param name="timestamp">Timestamp</param>
        void TimestampEvents(Time timestamp);

        /// <summary>
        /// Creates and persists the aggregate snapshot as an event
        /// </summary>
        void Snapshot();
    }
}