using System.Threading.Tasks;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Projections start sleeping, then process all events on rebuild
    /// </summary>
    public enum ProjectionStatus
    {
        /// <summary>
        /// Paused projection 
        /// </summary>
        Sleeping,
        
        /// <summary>
        /// Projection in rebuild state
        /// </summary>
        Building,
        
        /// <summary>
        /// Valid state, listening to incoming events 
        /// </summary>
        Listening
    }
    
    /// <summary>
    /// CQRS projections generated from a set of streams
    /// </summary>
    public interface IProjection 
    {
        /// <summary>
        /// Gets the task representing the projection rebuild
        /// </summary>
        /// <value>
        /// The task representing the projection rebuild
        /// </value>
        Task Complete { get; }

        /// <summary>
        /// Map stream to read model key
        /// </summary>
        /// <remarks>
        /// Projections with multiple read models require a key to separate by
        /// </remarks>
        /// <param name="stream">Originating stream</param>
        /// <returns>Read model key</returns>
        string Key(IStream stream);
    }

    /// <summary>
    /// Projections are singleton services which build the state from events
    /// sourced from a set of streams
    /// </summary>
    /// <typeparam name="TState">Type of the projection state</typeparam>
    public interface IProjection<out TState> : IProjection
    {
        /// <summary>
        /// Gets projection current state 
        /// </summary>
        /// <value>
        /// Projection current state 
        /// </value>
        TState State { get; }
    }
}