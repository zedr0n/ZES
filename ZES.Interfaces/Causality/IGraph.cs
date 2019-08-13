using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Causality
{

    public enum GraphReadState
    {
        Sleeping,
        Pausing,
        Queued,
        Reading
    }
    
    /// <summary>
    /// Graph state enum
    /// </summary>
    public enum GraphState
    {
        /// <summary>
        /// No activity in graph
        /// </summary>
        Sleeping,
        
        /// <summary>
        /// Reading in progress
        /// </summary>
        Reading,
        
        /// <summary>
        /// Update in progress
        /// </summary>
        Updating
    }
    
    /// <summary>
    /// Velocity graph for the app
    /// </summary>
    public interface IGraph
    {
        /// <summary>
        /// Gets state observable
        /// </summary>
        IObservable<GraphState> State { get; }
        
        /// <summary>
        /// Gets number of outstanding read requests
        /// </summary>
        IObservable<int> ReadRequests { get; }

        /// <summary>
        /// Create a clean database with target schema
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task Initialize();

        /// <summary>
        /// Add the event node to the graph
        /// </summary>
        /// <param name="e">Event instance</param>
        void AddEvent(IEvent e);

        /// <summary>
        /// Query graph for the stream version
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<int> GetStreamVersion(string key);

        /// <summary>
        /// Pause graph activity ( emulating update )
        /// </summary>
        /// <param name="ms">Pause duration</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task Pause(int ms);
    }
}