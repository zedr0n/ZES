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
using ZES.Interfaces.Infrastructure;

namespace ZES;

/// <inheritdoc />
public class FlowCompletionService : IFlowCompletionService
{
    private readonly ILog _log;
    private readonly ConcurrentDictionary<Guid, FlowNode> _flowNodes = new();
    private readonly ConcurrentDictionary<Guid, FlowNode> _uncompletedNodes = new();
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
        var newFlowNode = new FlowNode(_log) { Id = id, MessageId = message.MessageId, IsRetroactive = message is IRetroactiveCommand, Timeline = message.Timeline };
        var flowNode = _flowNodes.AddOrUpdate(id, newFlowNode, (key, existingNode) => 
            existingNode.IsCompleted ? newFlowNode : existingNode);
        
        flowNode.MarkUncompleted();
        _uncompletedNodes[id] = flowNode;
        _log.Debug($"Tracking {message.MessageId}({flowNode.CompletionCounter})");
        flowNode.CompletionSubject.Subscribe(_ =>
        {
            _uncompletedNodes.TryRemove(id, out var node);
            _log.Debug($"Completed {message.MessageId}({flowNode.CompletionCounter})");
        });
        
        if (flowNode.IsRetroactive)
        {
            _retroactiveSubject.OnNext(true);
            flowNode.CompletionSubject.Subscribe(_ => _retroactiveSubject.OnNext(false));
        }

        if (message.AncestorId == null || !_flowNodes.TryGetValue(message.AncestorId.Id, out var parentNode)) 
            return;
        
        var counter = parentNode.AddChild(flowNode);
        _log.Debug($"Adding tracked child {message.MessageId} to {parentNode.MessageId}({counter})");
    }

    /// <inheritdoc />
    public void MarkComplete(IMessage message)
    {
        if (!_flowNodes.TryGetValue(message.MessageId.Id, out var flowNode)) 
            return;
        
        _log.Debug($"Completing {message.MessageId}({flowNode.CompletionCounter})");
        flowNode.MarkCompleted();
    }

    /// <inheritdoc />
    public void SetIgnore(IMessage message, bool ignore)
    {
        if (!_flowNodes.TryGetValue(message.MessageId.Id, out var flowNode)) 
            return;
        
        flowNode.IsIgnored = ignore;
    }

    /// <inheritdoc />
    public async Task NodeCompletionAsync(IMessage message)
    {
        if (_flowNodes.TryGetValue(message.MessageId.Id, out var flowNode))
            await flowNode.CompletionTask;  // Use cached task
        else
            throw new KeyNotFoundException($"FlowNode with ID {message} not found.");
    }

    /// <inheritdoc />
    public async Task CompletionAsync(string timeline = null, bool includeRetroactive = false)
    {
        var flowNodes = timeline != null
            ? _uncompletedNodes.Values.Where(node => node.Timeline == timeline)
            : _uncompletedNodes.Values;

        var allNodeTasks = flowNodes
            .Where(node => (includeRetroactive || !node.IsRetroactive) && !node.IsIgnored)
            .Select(node => node.CompletionTask)
            .ToList();
        if (allNodeTasks.Count == 0)
            return;
        await Task.WhenAll(allNodeTasks);
    }
}