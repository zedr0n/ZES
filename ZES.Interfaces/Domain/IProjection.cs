using System;
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

        /*
        /// <summary>
        /// Returns when projection is rebuilt
        /// </summary>
        /// <returns>Listening</returns>
        dynamic GetAwaiter();*/
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