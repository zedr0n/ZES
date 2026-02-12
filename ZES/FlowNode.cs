using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using ZES.Interfaces;

namespace ZES;

/// <summary>
/// Command-event flow node
/// </summary>
public class FlowNode
{
    private readonly ILog _log;
    private IDisposable _childrenCompletionSubscription;
    private volatile int _completionCounter;
    private volatile int _isCompleted;
    
    private readonly ConcurrentDictionary<Guid, FlowNode> _children  = new();
    private readonly ReplaySubject<IObservable<bool>> _allChildObservables = new();
    private readonly ReplaySubject<bool> _counterObservable = new(1);
    
    /// <summary>
    /// Gets the completion counter
    /// </summary>
    public int CompletionCounter => _completionCounter;
    
    /// <summary>
    /// 
    /// </summary>
    public bool IsRetroactive { get; init; }
    
    /// <summary>
    /// Gets the value indicating whether the node has completed
    /// </summary>
    public bool IsCompleted => _isCompleted == 1; 

    /// <summary>
    /// Gets or sets the message identifier
    /// <seealso cref="IMessageMetadata.MessageId"/>
    /// </summary>
    public Guid Id { get; init; }
    
    /// <summary>
    /// Gets or sets the message identifier
    /// <seealso cref="IMessageMetadata.MessageId"/>
    /// </summary>
    public MessageId MessageId { get; init; }
    
    /// <summary>
    /// Gets or sets the message timeline
    /// </summary>
    public string Timeline { get; init; }

    /// <summary>
    /// Gets the completion subject
    /// </summary>
    public ReplaySubject<bool> CompletionSubject { get; } = new(1);

    private Task _completionTask;

    /// <summary>
    /// Gets the completion task (cached to avoid creating multiple subscriptions)
    /// </summary>
    public Task CompletionTask => _completionTask ??= CompletionSubject.FirstAsync().ToTask();

    /// <summary>
    ///
    /// </summary>
    /// <param name="log"></param>
    public FlowNode(ILog log)
    {
        _log = log;
        MonitorChildrenCompletion();
    }
    
    /// <summary>
    /// Add child and recheck completion if necessary
    /// </summary>
    /// <param name="child">Child node</param>
    public int AddChild(FlowNode child)
    {
        if (_isCompleted == 1)
        {
            _log.Errors.Add(new InvalidOperationException("Children added to a flow node after completion"));
            return 0;
        }

        if (_children.TryAdd(child.Id, child))
            _allChildObservables.OnNext(child.CompletionSubject.AsObservable()); 
        return _completionCounter;
    }    
    
    /// <summary>
    /// Request node uncompletion
    /// </summary>
    public void MarkUncompleted()
    {
        Interlocked.Increment(ref _completionCounter);    
        _counterObservable.OnNext(false);
    }
    
    /// <summary>
    /// Request node completion
    /// </summary>
    public void MarkCompleted()
    {
        if (Interlocked.Decrement(ref _completionCounter) > 0)
            return;
        
        _allChildObservables.OnNext(Observable.Return(true));
        _counterObservable.OnNext(true);
    }
    
    /// <summary>
    /// Emits when all children complete, dynamically updating when new children are added.
    /// </summary>
    /// <returns>Observable</returns>
    private IObservable<bool> ChildrenCompletionObservable()
    {
        var childCompletionObservable = _allChildObservables
            .Scan(new List<IObservable<bool>>(), (accumulated, current) =>
            {
                accumulated.Add(current);
                return accumulated;
            })
            .Select(observables => observables.CombineLatest()) // Combine all observables in the list
            .Switch() // Flatten the combined observables into a single stream
            .Select(_ => true); // Emit `true` when all are complete        
        
        return childCompletionObservable
            .CombineLatest(_counterObservable, (_, counterComplete) => counterComplete)
            .DistinctUntilChanged();
    }
    
    /// <summary>
    /// Starts monitoring when all children complete, but only if not already started.
    /// </summary>
    private void MonitorChildrenCompletion()
    {
        // Ensure monitoring starts only once
        _childrenCompletionSubscription ??= ChildrenCompletionObservable()
            .Subscribe(allComplete =>
            {
                if (allComplete)
                    CompleteNode();
            });
    }    
    
    private void CompleteNode()
    {
        if (Interlocked.CompareExchange(ref _isCompleted, 1, 0) == 1)
            return;        
        
        CompletionSubject.OnNext(true);
        CompletionSubject.OnCompleted();
    }
}