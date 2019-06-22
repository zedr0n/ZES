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
        Failed
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
        /// Map stream to read model key
        /// </summary>
        /// <remarks>
        /// Projections with multiple read models require a key to separate by
        /// </remarks>
        /// <param name="stream">Originating stream</param>
        /// <returns>Read model key</returns>
        string Key(IStream stream);

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