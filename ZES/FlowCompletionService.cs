using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES;

/// <inheritdoc />
public class FlowCompletionService : IFlowCompletionService
{
    private readonly ILog _log;
    private readonly ConcurrentDictionary<Guid, FlowNode> _flowNodes = new();
    private readonly ReplaySubject<bool> _retroactiveSubject = new(1);

    /// <inheritdoc />
    public IObservable<bool> RetroactiveExecution => _retroactiveSubject.AsObservable();
   
    /// <summary>
    /// 
    /// </summary>
    /// <param name="log">Error log</param>
    public FlowCompletionService(ILog log)
    {
        _log = log;
        _retroactiveSubject.OnNext(false);
    }
    
    /// <inheritdoc />
    public void TrackMessage(IMessage message)
    {
        var id = message.MessageId.Id;
        var newFlowNode = new FlowNode(_log) { Id = id, IsRetroactive = message is IRetroactiveCommand, Timeline = message.Timeline };
        var flowNode = _flowNodes.AddOrUpdate(id, newFlowNode, (key, existingNode) => 
            existingNode.IsCompleted ? newFlowNode : existingNode);
        
        flowNode.MarkUncompleted();
        _log.Trace($"Tracking {message.MessageId}({flowNode.CompletionCounter})");
        
        if (flowNode.IsRetroactive)
        {
            _retroactiveSubject.OnNext(true);
            flowNode.CompletionSubject.Subscribe(_ => _retroactiveSubject.OnNext(false));
        }

        if (message.AncestorId == null || !_flowNodes.TryGetValue(message.AncestorId.Id, out var parentNode)) 
            return;
        
        parentNode.AddChild(flowNode);
        parentNode.ChildrenCompletionObservable().Subscribe(_ => parentNode.CheckCompletion());
    }

    /// <inheritdoc />
    public void MarkComplete(IMessage message)
    {
        if (!_flowNodes.TryGetValue(message.MessageId.Id, out var flowNode)) 
            return;
        
        flowNode.MarkCompleted();
        _log.Trace($"Completing {message.MessageId}({flowNode.CompletionCounter})");
    }

    /// <inheritdoc />
    public async Task NodeCompletionAsync(IMessage message)
    {
        //var flowNodes = _flowNodes.GetOrAdd(message.Timeline, _ => new ConcurrentDictionary<Guid, FlowNode>());

        if (_flowNodes.TryGetValue(message.MessageId.Id, out var flowNode))
            await flowNode.CompletionSubject.FirstAsync().ToTask();  // Await completion
        else
            throw new KeyNotFoundException($"FlowNode with ID {message} not found.");
    }

    /// <inheritdoc />
    public async Task CompletionAsync(string timeline = null, bool includeRetroactive = false)
    {
        var flowNodes = timeline != null ? _flowNodes.Values.Where(node => node.Timeline == timeline) : _flowNodes.Values;

        var allNodeObservables = flowNodes.Where(node => includeRetroactive || node.IsRetroactive == false).Select(node => node.CompletionSubject.FirstAsync()).ToList();
        if (allNodeObservables.Count == 0)
            return;
        await allNodeObservables.Merge().LastAsync().ToTask();  
    }
}