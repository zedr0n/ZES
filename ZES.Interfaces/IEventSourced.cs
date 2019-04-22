using System.Collections.Generic;

namespace ZES.Interfaces
{
    public interface IEventSourced
    {
        /// <summary>
        /// Gets unique identifier
        /// </summary>
        string Id { get; }
        
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
        /// Hydrate the event sourced instance from event sequence
        /// </summary>
        /// <param name="pastEvents">Past event sequence</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        void LoadFrom<T>(IEnumerable<IEvent> pastEvents)
            where T : class, IEventSourced;
    }
    
    public interface IAggregate : IEventSourced { }
}