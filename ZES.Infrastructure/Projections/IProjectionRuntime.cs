using System.Collections.Concurrent;
using System.Threading;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Infrastructure;
// ReSharper disable PossibleInterfaceMemberAmbiguity

namespace ZES.Infrastructure.Projections;

/// <summary>
/// Represents a projection that owns stream processing, event application, and stream version tracking.
/// </summary>
/// <typeparam name="TState">
/// The projection state type.
/// </typeparam>
public interface IProjectionRuntime<out TState> : IProjectionSink<TState>, IProjection<TState>
{
    /// <summary>
    /// Gets the event store used to read events for projection rebuilds and live updates.
    /// </summary>
    IEventStore<IAggregate> EventStore { get; }

    /// <summary>
    /// Gets the logger used by the projection runtime and child flows.
    /// </summary>
    ILog Log { get; }

    /// <summary>
    /// Gets the cancellation token shared by the projection rebuild and its child processing flows.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the stream versions already processed by this projection runtime.
    /// </summary>
    ConcurrentDictionary<string, int> Versions { get; }
}
