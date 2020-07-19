using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Event-sourced repository
    /// </summary>
    public interface IEsRepository
    {
        /// <summary>
        /// Check if the root is valid 
        /// </summary>
        /// <param name="type">Event sourced type</param>
        /// <param name="id">Event sourced guid</param>
        /// <returns>True if valid, otherwise false</returns>
        Task<bool> IsValid(string type, string id);
        
        /// <summary>
        /// Gets last valid version of the event sourced instance 
        /// </summary>
        /// <param name="type">Event sourced type</param>
        /// <param name="id">Event sourced guid</param>
        /// <returns>True if valid, otherwise false</returns>
        Task<int> LastValidVersion(string type, string id);
        
        /// <summary>
        /// Gets invalid events in the event sourced instance
        /// </summary>
        /// <param name="type">Event sourced type</param>
        /// <param name="id">Event soured guid</param>
        /// <returns>List of invalid events</returns>
        Task<IEnumerable<IEvent>> FindInvalidEvents(string type, string id);
    }
    
    /// <summary>
    /// Event-sourced repository
    /// </summary>
    /// <typeparam name="TEventSourced">Event-sourced instance type</typeparam>
    public interface IEsRepository<in TEventSourced> : IEsRepository
        where TEventSourced : IEventSourced
    {
        /// <summary>
        /// Saves the events to event store and publish those to event bus
        /// </summary>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <param name="es">The event sourced instance</param>
        /// <returns>Task representing the save operation</returns>
        Task Save<T>(T es)
            where T : class, TEventSourced;

        /// <summary>
        /// Get or add new event sourced instance
        /// </summary>
        /// <param name="id">Event sourced id</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Task representing the hydrated event sourced instance</returns>
        Task<T> GetOrAdd<T>(string id)
            where T : class, TEventSourced, new();
        
        /// <summary>
        /// Rebuild from event history extracted from Event Store
        /// </summary>
        /// <param name="id">Event sourced guid</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Aggregate/saga or null if no events found</returns>
        Task<T> Find<T>(string id)
            where T : class, TEventSourced, new();
        
        /// <summary>
        /// Check if the root is valid 
        /// </summary>
        /// <param name="id">Event sourced guid</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>True if valid, otherwise false</returns>
        Task<bool> IsValid<T>(string id)
            where T : class, TEventSourced, new();

        /// <summary>
        /// Gets last valid version of the event sourced instance 
        /// </summary>
        /// <param name="id">Event sourced guid</param>
        /// <typeparam name="T">Event source type</typeparam>
        /// <returns>True if valid, otherwise false</returns>
        Task<int> LastValidVersion<T>(string id)
            where T : class, TEventSourced, new();

        /// <summary>
        /// Gets invalid events in the event sourced instance
        /// </summary>
        /// <param name="id">Event soured guid</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>List of invalid events</returns>
        Task<IEnumerable<IEvent>> FindInvalidEvents<T>(string id) 
            where T : class, TEventSourced, new();
    }
}
