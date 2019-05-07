using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
  /// <summary>
  /// Provides an abstract flow that dispatch inputs to multiple child flows by a special dispatch function, which is
  /// useful in situations that you want to group inputs by a certain property and let specific child flows to take
  /// care of different groups independently. DataDispatcher also helps creating and maintaining child flows dynamically
  /// in a thread-safe way.
  /// </summary>
  /// <typeparam name="TKey">Type of the dispatch key to group input items</typeparam>
  /// <typeparam name="TIn">Type of input items of this dispatcher flow</typeparam>
  /// <typeparam name="TOut">Flow control output</typeparam>
  /// <remarks>
  /// This flow guarantees an input goes to only ONE of the child flows. Notice the difference comparing to DataBroadcaster, which
  /// gives the input to EVERY flow it is linked to.
  /// </remarks>
  public abstract class ParallelDataDispatcher<TKey, TIn, TOut> : Dataflow<TIn>
  {
    private readonly Func<TIn, TKey> _dispatchFunc;
    private readonly ActionBlock<TIn> _dispatcherBlock;
    private readonly ConcurrentDictionary<TKey, Lazy<Dataflow<TIn, TOut>>> _destinations;
    private readonly ConcurrentDictionary<TKey, Lazy<Dataflow<TOut, TOut>>> _outputs;
    private readonly Func<TKey, Lazy<Dataflow<TIn, TOut>>> _initer;
    private readonly Func<TKey, Lazy<Dataflow<TOut, TOut>>> _outIniter;
    private readonly Type _declaringType;
    
    private int _parallelCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelDataDispatcher{TIn, TOut, TKey}"/> class.Construct an DataDispatcher instance
    /// </summary>
    /// <param name="dispatchFunc">The dispatch function</param>
    protected ParallelDataDispatcher(Func<TIn, TKey> dispatchFunc)
      : this(dispatchFunc, DataflowOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelDataDispatcher{TIn, TOut, TKey}"/> class.
    /// </summary>
    /// <param name="dispatchFunc">The dispatch function</param>
    /// <param name="options">Option for this dataflow</param>
    /// <param name="declaringType">Runtime type of the owner of this dispatcher</param>
    protected ParallelDataDispatcher(Func<TIn, TKey> dispatchFunc, DataflowOptions options, Type declaringType = null)
      : this(dispatchFunc, EqualityComparer<TKey>.Default, options, declaringType)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParallelDataDispatcher{TIn, TOut, TKey}"/> class.Construct an DataDispatcher instance</summary>
    /// <param name="dispatchFunc">The dispatch function</param>
    /// <param name="keyComparer">The key comparer for this dataflow</param>
    /// <param name="option">Option for this dataflow</param>
    /// <param name="declaringType">Runtime type of the owner of this dispatcher</param>
    private ParallelDataDispatcher(
      Func<TIn, TKey> dispatchFunc,
      IEqualityComparer<TKey> keyComparer,
      DataflowOptions option,
      Type declaringType)
      : base(option)
    {
      _dispatchFunc = dispatchFunc;
      _declaringType = declaringType;
      _destinations = new ConcurrentDictionary<TKey, Lazy<Dataflow<TIn, TOut>>>(keyComparer);
      _outputs = new ConcurrentDictionary<TKey, Lazy<Dataflow<TOut, TOut>>>();
      _initer = key => new Lazy<Dataflow<TIn, TOut>>(() =>
      {
        var childFlow = CreateChildFlow(key);
        RegisterChild(childFlow);
        childFlow.RegisterDependency(_dispatcherBlock);
        return childFlow;
      });
      
      _outIniter = key => new Lazy<Dataflow<TOut, TOut>>(() =>
      {
        var childFlow = new OutputFlow(DataflowOptions); 
        RegisterChild(childFlow);
        childFlow.RegisterDependency(_dispatcherBlock);
        return childFlow;
      });

      _dispatcherBlock = new ActionBlock<TIn>( async input => await Dispatch(input), option.ToExecutionBlockOption(true));

      RegisterChild(_dispatcherBlock);
    }

    /// <summary>
    /// Gets current number of parallel actions executing 
    /// </summary>
    /// <value>
    /// Current number of parallel actions executing 
    /// </value>
    public int ParallelCount => _parallelCount;
    
    /// <summary>
    /// Gets <see cref="P:Gridsum.DataflowEx.Dataflow`1.InputBlock" />
    /// </summary>
    /// <value><see cref="P:Gridsum.DataflowEx.Dataflow`1.InputBlock" /></value>
    public override ITargetBlock<TIn> InputBlock => _dispatcherBlock;

    internal ILog Log { get; set; }
    
    /// <summary>
    /// Binds an action on completion fault 
    /// </summary>
    /// <param name="action">Action to perform</param>
    /// <returns>Fluent instance</returns>
    public ParallelDataDispatcher<TKey, TIn, TOut> OnError(Action<Task> action)
    {
      CompletionTask.ContinueWith(t => t.IsFaulted ? action : null);
      return this;
    }

    /// <summary>
    /// Every child flow will have an attached buffer block containing its outputs which can be accessed or awaited 
    /// </summary>
    /// <param name="key">Dispatch key</param>
    /// <returns>Buffer block containing the outputs from child dataflows</returns>
    protected ISourceBlock<TOut> OutputBlock(TKey key) => _outputs.GetOrAdd(key, _outIniter).Value.OutputBlock;
    
    /// <summary>Create the child flow on-the-fly by the dispatch key</summary>
    /// <param name="dispatchKey">The unique key to create and identify the child flow</param>
    /// <returns>A new child dataflow which is responsible for processing items having the given dispatch key</returns>
    /// <remarks>
    /// The dispatch key should have a one-one relationship with child flow
    /// </remarks>
    protected abstract Dataflow<TIn, TOut> CreateChildFlow(TKey dispatchKey);
    
    private async Task Dispatch(TIn input)
    {
      Interlocked.Increment(ref _parallelCount);
      Log?.Trace($"Parallel {typeof(TIn).GetFriendlyName()} count : {_parallelCount}", (_declaringType ?? GetType().DeclaringType)?.GetFriendlyName());

      var copy = input.ToString();
      
      var key = _dispatchFunc(input);
      var block = _destinations.GetOrAdd(key, _initer).Value;

      await block.SendAsync(input);
      
      var timeout = DataflowOptions is DataflowOptionsEx optionEx ? optionEx.Timeout : TimeSpan.FromMilliseconds(-1);
      try
      {
        var output = await block.OutputBlock.ReceiveAsync(timeout);
        var outBlock = _outputs.GetOrAdd(key, _outIniter).Value;
        await outBlock.SendAsync(output);
        Log?.Debug($"{copy} -> {output}", (_declaringType ?? GetType().DeclaringType)?.GetFriendlyName());
      }
      catch (Exception)
      {
        // ignored
      }

      Interlocked.Decrement(ref _parallelCount); 
    }
    
    private class OutputFlow : Dataflow<TOut, TOut>
    {
      private readonly BufferBlock<TOut> _buffer;
      public OutputFlow(DataflowOptions dataflowOptions)
        : base(dataflowOptions)
      {
        _buffer = new BufferBlock<TOut>();
       
        RegisterChild(_buffer);
      }

      public override ITargetBlock<TOut> InputBlock => _buffer;
      public override ISourceBlock<TOut> OutputBlock => _buffer;
      public override void Complete()
      {
        _buffer.TryReceiveAll(out _);
        base.Complete();
      }
    }
  }
}