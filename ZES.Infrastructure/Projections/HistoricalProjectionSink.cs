using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Projections;

/// <summary>
/// Projection sink that builds state as it existed at a specific historical timestamp.
/// </summary>
/// <typeparam name="TState">
/// The projection state type maintained by the sink.
/// </typeparam>
/// <remarks>
/// Events with timestamps later than <see cref="Timestamp"/> are ignored.
/// </remarks>
public class HistoricalProjectionSink<TState> : ProjectionSink<TState>
    where TState : new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HistoricalProjectionSink{TState}"/> class.
    /// </summary>
    /// <param name="source">Projection whose handlers and cancellation token are used by the sink.</param>
    public HistoricalProjectionSink(IProjection<TState> source) : base(source)
    {
    }

    /// <summary>
    /// Gets or sets the historical cutoff timestamp for this sink.
    /// </summary>
    /// <remarks>
    /// Events after this timestamp are skipped while rebuilding the sink state.
    /// </remarks>
    public Time Timestamp { get; set; }

    /// <summary>
    /// Gets the latest event timestamp accepted by this sink.
    /// </summary>
    public override Time Latest => Timestamp;
}
