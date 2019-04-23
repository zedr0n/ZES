using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
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
    }

    /// <summary>
    /// CQRS projection with a set state generated from a set of streams
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