namespace ZES.Interfaces.Branching;

/// <summary>
/// Represents a timeline that can track and manage the currently active timeline.
/// </summary>
public interface IActiveTimeline : ITimeline
{
    /// <summary>
    /// Gets or sets the currently active timeline within the context of timeline management.
    /// </summary>
    /// <remarks>
    /// This property represents the active timeline being tracked or managed.
    /// It can be used to determine or update the timeline that is currently designated as active.
    /// </remarks>
    ITimeline Timeline { get; set; }
}