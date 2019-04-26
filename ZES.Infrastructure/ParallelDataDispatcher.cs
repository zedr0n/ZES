using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;

namespace ZES.Infrastructure
{
  /// <summary>
  /// Provides an abstract flow that dispatch inputs to multiple child flows by a special dispatch function, which is
  /// useful in situations that you want to group inputs by a certain property and let specific child flows to take
  /// care of different groups independently. DataDispatcher also helps creating and maintaining child flows dynamically
  /// in a thread-safe way.
  /// </summary>
  /// <typeparam name="TIn">Type of input items of this dispatcher flow</typeparam>
  /// <typeparam name="TKey">Type of the dispatch key to group input items</typeparam>
  /// <remarks>
  /// This flow guarantees an input goes to only ONE of the child flows. Notice the difference comparing to DataBroadcaster, which
  /// gives the input to EVERY flow it is linked to.
  /// </remarks>
  public abstract class ParallelDataDispatcher<TIn, TKey> : Dataflow<TIn>
  {
    private readonly ActionBlock<TIn> _dispatcherBlock;
    private readonly ConcurrentDictionary<TKey, Lazy<Dataflow<TIn>>> _destinations;
    private readonly Func<TKey, Lazy<Dataflow<TIn>>> _initer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelDataDispatcher{TIn, TKey}"/> class.Construct an DataDispatcher instance
    /// </summary>
    /// <param name="dispatchFunc">The dispatch function</param>
    protected ParallelDataDispatcher(Func<TIn, TKey> dispatchFunc)
      : this(dispatchFunc, DataflowOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelDataDispatcher{TIn, TKey}"/> class.
    /// </summary>
    /// <param name="dispatchFunc">The dispatch function</param>
    /// <param name="option">Option for this dataflow</param>
    protected ParallelDataDispatcher(Func<TIn, TKey> dispatchFunc, DataflowOptions option)
      : this(dispatchFunc, EqualityComparer<TKey>.Default, option)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParallelDataDispatcher{TIn, TKey}"/> class.Construct an DataDispatcher instance</summary>
    /// <param name="dispatchFunc">The dispatch function</param>
    /// <param name="keyComparer">The key comparer for this dataflow</param>
    /// <param name="option">Option for this dataflow</param>
    protected ParallelDataDispatcher(
      Func<TIn, TKey> dispatchFunc,
      IEqualityComparer<TKey> keyComparer,
      DataflowOptions option)
      : base(option)
    {
      var parallelDataDispatcher = this;
      _destinations = new ConcurrentDictionary<TKey, Lazy<Dataflow<TIn>>>(keyComparer);
      _initer = key => new Lazy<Dataflow<TIn>>(() =>
      {
        var childFlow = CreateChildFlow(key);
        RegisterChild(childFlow);
        childFlow.RegisterDependency(_dispatcherBlock);
        return childFlow;
      });
      _dispatcherBlock = new ActionBlock<TIn>(
        async input =>
        {
          await SendToChild(
            parallelDataDispatcher._destinations.GetOrAdd(dispatchFunc(input), parallelDataDispatcher._initer).Value,
            input).ConfigureAwait(false);
        }, option.ToExecutionBlockOption(true));

      RegisterChild(_dispatcherBlock);
    }

    /// <summary>
    /// Gets <see cref="P:Gridsum.DataflowEx.Dataflow`1.InputBlock" />
    /// </summary>
    /// <value><see cref="P:Gridsum.DataflowEx.Dataflow`1.InputBlock" /></value>
    public override ITargetBlock<TIn> InputBlock => _dispatcherBlock;

    /// <summary>
    /// Send the input to dynamic child block
    /// </summary>
    /// <param name="dataflow">Input dataflow</param>
    /// <param name="input">Input value</param>
    /// <returns>Task representing the asynchronous operation of sending the input to the processing block</returns>
    protected virtual async Task SendToChild(Dataflow<TIn> dataflow, TIn input)
    {
      await dataflow.SendAsync(input);
    }

    /// <summary>Create the child flow on-the-fly by the dispatch key</summary>
    /// <param name="dispatchKey">The unique key to create and identify the child flow</param>
    /// <returns>A new child dataflow which is responsible for processing items having the given dispatch key</returns>
    /// <remarks>
    /// The dispatch key should have a one-one relationship with child flow
    /// </remarks>
    protected abstract Dataflow<TIn> CreateChildFlow(TKey dispatchKey);
  }
}