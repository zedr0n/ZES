using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Projections;

/// <summary>
/// Projection sink that reuses a projection's event handlers to build an auxiliary state.
/// </summary>
/// <typeparam name="TState">The projection state type maintained by the sink.</typeparam>
public class ProjectionSink<TState> : IProjectionSink<TState>
    where TState : new()
{
    private readonly ProjectionBase<TState> _source;
    private readonly Dictionary<Type, Func<IEvent, TState, TState>> _handlers;
    private readonly ActionBlock<Tracked<IEvent>> _updateStateBlock;
    private int _parallel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionSink{TState}"/> class.
    /// </summary>
    /// <param name="source">Projection whose handlers and cancellation token are used by the sink.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="source"/> is not backed by <see cref="ProjectionBase{TState}"/>.
    /// </exception>
    protected ProjectionSink(IProjection<TState> source)
    {
        _source = source as ProjectionBase<TState> ?? throw new InvalidOperationException("Projection must be derived from ProjectionBase"); 
        _handlers = _source.Handlers.ToDictionary(x => x.Key, x => x.Value);
        State = new TState();
        
        _updateStateBlock = new ActionBlock<Tracked<IEvent>>(
            UpdateState,
            Configuration.DataflowOptions.ToDataflowBlockOptions(false));
    }

    /// <inheritdoc />
    public TState State { get; private set; }

    /// <inheritdoc />
    public virtual Time Latest => _source.Latest;

    /// <inheritdoc />
    public Task When(IEvent e)
    {
        if (_source.CancellationToken.IsCancellationRequested || e == null)
            return Task.CompletedTask;

        var tracked = new Tracked<IEvent>(e);
        _source.CancellationToken.Register(() => tracked.Complete());

        Interlocked.Increment(ref _parallel);
        _updateStateBlock.Post(tracked);

        return tracked.Task;
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (State)
        {
            State = new TState();
        }
    }

    private void UpdateState(Tracked<IEvent> tracked)
    {
        if (tracked.Task.IsCompleted)
        {
            Interlocked.Decrement(ref _parallel);
            return;
        }

        var e = tracked.Value;

        if (_handlers.TryGetValue(e.GetType(), out var handler) && e.Timestamp <= Latest)
            State = handler(e, State);

        Interlocked.Decrement(ref _parallel);
        tracked.Complete();
    }
}
