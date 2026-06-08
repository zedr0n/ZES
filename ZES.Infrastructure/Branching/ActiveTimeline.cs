using NodaTime;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Branching;

/// <inheritdoc />
public class ActiveTimeline(ITimeline timeline) : IActiveTimeline
{
    /// <inheritdoc />
    public string Id => Timeline.Id;

    /// <inheritdoc />
    public Time Now => Timeline.Now;

    /// <inheritdoc />
    public bool Live => Timeline.Live;

    /// <inheritdoc />
    public void Warp(Time time) => Timeline.Warp(time);

    /// <inheritdoc />
    public void Advance(Period period) => Timeline.Advance(period);

    /// <inheritdoc />
    public ITimeline New(string id, Time time = default) => Timeline.New(id, time);

    /// <inheritdoc />
    public void QueueCommand(ICommand command) => Timeline.QueueCommand(command);

    /// <inheritdoc />
    public ICommand DequeCommand() => Timeline.DequeCommand();

    /// <inheritdoc />
    public ICommand PeekCommand() => Timeline.PeekCommand();

    /// <inheritdoc />
    public ITimeline Timeline { get; set; } = timeline;
}