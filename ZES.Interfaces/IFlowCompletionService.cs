using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZES.Interfaces;

/// <summary>
/// Service to track flow completion
/// </summary>
public interface IFlowCompletionService
{
    /// <summary>
    /// Gets the observable which emits the status of retroactive execution in the systems 
    /// </summary>
    IObservable<bool> RetroactiveExecution { get; }

    /// <summary>
    /// Add node for tracking
    /// </summary>
    /// <param name="message">Message to track</param>
    void TrackMessage(IMessage message);

    /// <summary>
    /// Mark flow node for completion
    /// </summary>
    /// <param name="message">Message to track</param>
    void MarkComplete(IMessage message);

    /// <summary>
    /// Await the completion of a specific node by its ID asynchronously
    /// </summary>
    /// <param name="message">Message to track completion</param>
    /// <exception cref="KeyNotFoundException">Thrown if message is not tracked</exception>
    /// <returns><see cref="Task"/> representing the node completion</returns>
    public Task NodeCompletionAsync(IMessage message);

    /// <summary>
    /// Await the completion of all nodes 
    /// </summary>
    /// <param name="timeline">Timeline to await completion for</param>
    /// <param name="includeRetroactive">Include retroactive nodes to await</param>
    /// <returns><see cref="Task"/> representing the completion</returns>
    public Task CompletionAsync(string timeline = null, bool includeRetroactive = false);
}
