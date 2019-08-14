using System.Threading.Tasks;

namespace ZES.Interfaces.Causality
{
    /// <summary>
    /// Update interface for graph
    /// </summary>
    public interface IUpdateGraph
    {
        /// <summary>
        /// Create graph schema
        /// </summary>
        void Initialize();

        /// <summary>
        /// Pause all graph operations for <paramref name="ms"/>
        /// </summary>
        /// <param name="ms">Number of milliseconds to pause for</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task Pause(int ms);

        /// <summary>
        /// Add new event node 
        /// </summary>
        /// <param name="e">Event instance</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task AddEvent(IEvent e);
    }
}