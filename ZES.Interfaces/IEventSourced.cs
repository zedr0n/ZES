using System;
using System.Collections.Generic;

namespace ZES.Interfaces
{
    public interface IEventSourced
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        Guid Id { get; }
        
        /// <summary>
        /// Aggregate version ( for optimistic concurrency )
        /// </summary>
        long Version { get; }

        /// <summary>
        /// Events not yet committed 
        /// </summary>
        IEvent[] GetUncommittedEvents();

        /// <summary>
        /// Hydrate the event sourced instance from event sequence
        /// </summary>
        /// <param name="id">Unique identifier for event sourced instance</param>
        /// <param name="pastEvents">Past event sequence</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Hydrated event sourced instance</returns>
        void LoadFrom<T>(Guid id,IEnumerable<IEvent> pastEvents) where T : class, IEventSourced;
    }
}