using System.Collections.Generic;

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
        /// <value>
        /// Unique string identifier
        /// </value>
        string Id { get; }

        /// <summary>
        /// Gets event sourced version ( for optimistic concurrency )
        /// </summary>
        /// <value>
        /// Event sourced version 
        /// </value>
        int Version { get; }
        
        /// <summary>
        /// Gets events not yet committed 
        /// </summary>
        /// <returns>Sequence of events</returns>
        IEnumerable<IEvent> GetUncommittedEvents();

        /// <summary>
        /// Hydrate the event sourced instance from event sequence
        /// </summary>
        /// <param name="pastEvents">Past event sequence</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        void LoadFrom<T>(IEnumerable<IEvent> pastEvents)
            where T : class, IEventSourced;
    }
}