using System;
using System.Collections.Generic;

namespace ZES.Interfaces
{
    public interface IEventSourced
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Aggregate version ( for optimistic concurrency )
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Events not yet committed 
        /// </summary>
        IEvent[] GetUncommittedEvents();

        /// <summary>
        /// Hydrate the event sourced instance from event sequence
        /// </summary>
        /// <param name="pastEvents">Past event sequence</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Hydrated event sourced instance</returns>
        void LoadFrom<T>(IEnumerable<IEvent> pastEvents) where T : class, IEventSourced;
    }
    
    public interface IAggregate : IEventSourced {}
}