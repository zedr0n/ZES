using System.Threading.Tasks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces.Causality
{
    /// <summary>
    /// Update interface for graph
    /// </summary>
    public interface IGraph
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

        /// <summary>
        /// Add new command node
        /// </summary>
        /// <param name="command">Command</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task AddCommand(ICommand command);

        /// <summary>
        /// Add stream metadata node
        /// </summary>
        /// <param name="stream">Stream info</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task AddStreamMetadata(IStream stream);
    }
}