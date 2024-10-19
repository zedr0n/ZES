using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ZES.Interfaces;

namespace ZES;

/// <summary>
/// Command-event flow node
/// </summary>
public class FlowNode(ILog log)
{
    private int _completionCounter;
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
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets or sets the message identifier
    /// <seealso cref="IMessageMetadata.MessageId"/>
    /// </summary>
    public Guid Id { get; init; }
    
    /// <summary>
    /// Gets or sets the message timeline
    /// </summary>
    public string Timeline { get; init; }

    /// <summary>
    /// Gets the node children
    /// </summary>
    private ConcurrentDictionary<Guid, FlowNode> Children { get; } = new();

    /// <summary>
    /// Gets the completion subject
    /// </summary>
    public ReplaySubject<bool> CompletionSubject { get; } = new(1);

    /// <summary>
    /// Emits when all children complete
    /// </summary>
    /// <returns>Observable</returns>
    public IObservable<bool> ChildrenCompletionObservable()
    {
        var childObservables = Children.Values.Select(c => c.CompletionSubject.AsObservable());
        return childObservables.CombineLatest().Select(_ => true);
    }
    
    /// <summary>
    /// Add child and recheck completion if necessary
    /// </summary>
    /// <param name="child">Child node</param>
    public void AddChild(FlowNode child)
    {
        if (IsCompleted)
        {
            log.Errors.Add(new InvalidOperationException("Children added to a flow node after completion"));
            return;
        }

        Children.TryAdd(child.Id, child);
        
        // Check completion immediately if completion was requested
        if (_completionCounter <= 0)
            CheckCompletion();
    }    
    
    /// <summary>
    /// Request node uncompletion
    /// </summary>
    public void MarkUncompleted()
    {
        Interlocked.Increment(ref _completionCounter);    
    }
    
    /// <summary>
    /// Request node completion
    /// </summary>
    public void MarkCompleted()
    {
        Interlocked.Decrement(ref _completionCounter);
        
        if(_completionCounter <= 0 && Children.Values.All(c => c.IsCompleted))
            CompleteNode();
    }
    
    /// <summary>
    /// Check the node for completion
    /// </summary>
    public void CheckCompletion()
    {
        if(_completionCounter <= 0 && Children.Values.All(c => c.IsCompleted))
            CompleteNode();
    }
    
    private void CompleteNode()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
        CompletionSubject.OnNext(true);
        CompletionSubject.OnCompleted();
    }
}