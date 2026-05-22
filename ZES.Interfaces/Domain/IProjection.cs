using System;
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
        /// Projection execution being cancelled
        /// </summary>
        Cancelling,
        
        /// <summary>
        /// Valid state, listening to incoming events 
        /// </summary>
        Listening,
        
        /// <summary>
        /// Projection failed 
        /// </summary>
        Failed,
    }
    
    /// <summary>
    /// CQRS projections generated from a set of streams
    /// </summary>
    public interface IProjection
    {
        /// <summary>
        /// Gets completion of this observable means the projection's rebuild has been completed successfully
        /// </summary>
        /// <value>
        /// Completion of this observable means the projection's rebuild has been completed successfully
        /// </value>
        IObservable<ProjectionStatus> Ready { get; }

        /// <summary>
        /// Gets or sets the stream predicate
        /// </summary>
        Func<string, bool> StreamIdPredicate { get; set; }
        
        /// <summary>
        /// Gets or sets the stream predicate
        /// </summary>
        Func<IStream, bool> Predicate { get; set; }

        /// <summary>
        /// Gets the projection identifier 
        /// </summary>
        Guid Guid { get; }
        
        /// <summary>
        /// Gets or sets the projection timeline
        /// </summary>
        string Timeline { get; set; }

        /// <summary>
        /// Resets the state of the projection and starts a process to completely rebuild its data.
        /// </summary>
        /// <remarks>
        /// This method clears the current state of the projection and triggers a rebuild based on the
        /// configured data streams. Typically used when the projection needs to be refreshed or brought
        /// in sync with changes in the underlying data framework.
        /// </remarks>
        /// <returns>
        /// A task representing the asynchronous operation of resetting the projection state and starting the rebuild process.
        /// </returns>
        public Task Restart();

        /// <summary>
        /// Releases all resources used by the <see cref="IProjection"/> implementation.
        /// </summary>
        /// <remarks>
        /// This method is called to explicitly clean up resources such as connections,
        /// subscriptions, and other unmanaged resources associated with the projection.
        /// It is used to ensure appropriate cleanup and should be invoked when the projection
        /// is no longer needed, especially in scenarios like handling historical timelines
        /// or disposing projections created dynamically during query execution.
        /// </remarks>
        public void Dispose();
    }

    /// <summary>
    /// Read-only view of a projection state.
    /// </summary>
    /// <typeparam name="TState">Type of the projection state.</typeparam>
    public interface IProjectionState<out TState>
    {
        /// <summary>
        /// Gets the current projection state.
        /// </summary>
        TState State { get; }
    }
    
    /// <summary>
    /// Projections are singleton services which build the state from events
    /// sourced from a set of streams
    /// </summary>
    /// <typeparam name="TState">Type of the projection state</typeparam>
    public interface IProjection<out TState> : IProjection, IProjectionState<TState>
    {
    }
}
