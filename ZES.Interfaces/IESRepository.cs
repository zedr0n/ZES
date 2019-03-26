using System.Threading.Tasks;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces
{
    public interface IEsRepository<in I> where I : IEventSourced 
    {
        /// <summary>
        /// Saves the events to event store and publish those to event bus
        /// </summary>
        /// <param name="es">The event sourced instance</param>
        Task Save<T>(T es) where T : class, I;

        /// <summary>
        /// Get or add new event sourced instance
        /// </summary>
        /// <param name="id"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<T> GetOrAdd<T>(string id) where T : class, I, new();
        
        /// <summary>
        /// Rebuild from event history extracted from Event Store
        /// </summary>
        /// <param name="id">Event sourced guid</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Aggregate/saga or null if no events found</returns>
        Task<T> Find<T>(string id) where T : class, I,new();
    }
}
