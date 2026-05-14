using System.Threading.Tasks;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces.Domain;

/// <summary>
/// Receives projection events and maintains an auxiliary projection state.
/// </summary>
/// <typeparam name="TState">The projection state type maintained by the sink.</typeparam>
public interface IProjectionSink<out TState> : IProjectionState<TState>
{
    /// <summary>
    /// Gets the latest event timestamp that should be applied to this sink.
    /// </summary>
    Time Latest { get; }
    
    /// <summary>
    /// Applies an event to the sink state when it is accepted by the sink.
    /// </summary>
    /// <param name="e">The event to apply.</param>
    /// <returns>A task that completes when the event has been processed.</returns>
    Task When(IEvent e);

    /// <summary>
    /// Resets the sink state to its initial value before a projection rebuild.
    /// </summary>
    void Reset();
}
