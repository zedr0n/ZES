using System;
using System.Threading.Tasks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Event-sourced repository
    /// </summary>
    /// <typeparam name="I">Event-sourced instance type</typeparam>
    public interface IEsRepository<in I>
        where I : IEventSourced
    {
        /// <summary>
        /// Saves the events to event store and publish those to event bus
        /// </summary>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <param name="es">The event sourced instance</param>
        /// <param name="ancestorId">Ancestor unique identifier</param>
        /// <returns>Task representing the save operation</returns>
        Task Save<T>(T es, Guid? ancestorId = null)
            where T : class, I;

        /// <summary>
        /// Get or add new event sourced instance
        /// </summary>
        /// <param name="id">Event sourced id</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Task representing the hydrated event sourced instance</returns>
        Task<T> GetOrAdd<T>(string id)
            where T : class, I, new();
        
        /// <summary>
        /// Rebuild from event history extracted from Event Store
        /// </summary>
        /// <param name="id">Event sourced guid</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Aggregate/saga or null if no events found</returns>
        Task<T> Find<T>(string id)
            where T : class, I, new();
    }
}
