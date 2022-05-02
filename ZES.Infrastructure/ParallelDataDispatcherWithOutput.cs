using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
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
    /// <remarks>
    /// This flow guarantees an input goes to only ONE of the child flows. Notice the difference comparing to DataBroadcaster, which
    /// gives the input to EVERY flow it is linked to.
    /// </remarks>
    public abstract class ParallelDataDispatcherWithOutput<TKey, TIn> : Dataflow<TIn, TIn>
    {
        private readonly Func<TIn, TKey> _dispatchFunc;
    
        private readonly ActionBlock<TIn> _dispatcherBlock;
        private readonly BufferBlock<TIn> _completedBlock;

        private readonly ConcurrentDictionary<TKey, Lazy<Dataflow<TIn, TIn>>> _destinations;
        private readonly Func<TKey, Lazy<Dataflow<TIn, TIn>>> _initer;
    
        private readonly Type _declaringType;
        private readonly CancellationToken _token;
    
        private int _parallelCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParallelDataDispatcherWithOutput{TKey, TIn}"/> class.
        /// </summary>
        /// <param name="dispatchFunc">The dispatch function</param>
        /// <param name="options">Option for this dataflow</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="declaringType">Runtime type of the owner of this dispatcher</param>
        protected ParallelDataDispatcherWithOutput(Func<TIn, TKey> dispatchFunc, DataflowOptions options, CancellationToken token, Type declaringType = null)
            : this(dispatchFunc, EqualityComparer<TKey>.Default, options, token, declaringType)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ParallelDataDispatcherWithOutput{TKey, TIn}"/> class.Construct an DataDispatcher instance</summary>
        /// <param name="dispatchFunc">The dispatch function</param>
        /// <param name="keyComparer">The key comparer for this dataflow</param>
        /// <param name="option">Option for this dataflow</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="declaringType">Runtime type of the owner of this dispatcher</param>
        private ParallelDataDispatcherWithOutput(
            Func<TIn, TKey> dispatchFunc,
            IEqualityComparer<TKey> keyComparer,
            DataflowOptions option,
            CancellationToken token,
            Type declaringType)
            : base(option)
        {
            _token = token;
            _dispatchFunc = dispatchFunc;
            _declaringType = declaringType;
            _destinations = new ConcurrentDictionary<TKey, Lazy<Dataflow<TIn, TIn>>>(keyComparer);
            _initer = key => new Lazy<Dataflow<TIn, TIn>>(() =>
            {
                var childFlow = CreateChildFlow(key);
                RegisterChild(childFlow);
                childFlow.RegisterDependency(_dispatcherBlock);
                // childFlow.OutputBlock.LinkTo(_completedBlock);
                return childFlow;
            });

            _completedBlock = new BufferBlock<TIn>();
            _dispatcherBlock = new ActionBlock<TIn>(async input => await Dispatch(input), option.ToDataflowBlockOptions(true)); // .ToExecutionBlockOption(true));

            RegisterChild(_dispatcherBlock);
            RegisterChild(_completedBlock);
        }

        /// <summary>
        /// Gets current number of parallel actions executing 
        /// </summary>
        /// <value>
        /// Current number of parallel actions executing 
        /// </value>
        public int ParallelCount => _parallelCount;

        /// <inheritdoc />
        public override ITargetBlock<TIn> InputBlock => _dispatcherBlock;

        /// <inheritdoc />
        public override ISourceBlock<TIn> OutputBlock => _completedBlock;

        /// <summary>
        /// Gets or sets log services
        /// </summary>
        protected ILog Log { get; set; }

        /// <summary>Create the child flow on-the-fly by the dispatch key</summary>
        /// <param name="dispatchKey">The unique key to create and identify the child flow</param>
        /// <returns>A new child dataflow which is responsible for processing items having the given dispatch key</returns>
        /// <remarks>
        /// The dispatch key should have a one-one relationship with child flow
        /// </remarks>
        protected abstract Dataflow<TIn, TIn> CreateChildFlow(TKey dispatchKey);

        private async Task Dispatch(TIn input)
        {
            if (input == null)
                return;
            
            Interlocked.Increment(ref _parallelCount);
            Log?.Trace($"{(input is Tracked<IEvent> trackedEvent ? trackedEvent.Value.GetType().GetFriendlyName() : input.GetType().GetFriendlyName())}", (_declaringType ?? GetType().DeclaringType)?.GetFriendlyName());

            var key = _dispatchFunc(input);
            if (key == null)
            {
                await _completedBlock.SendAsync(input, _token);
                return;
            }

            var block = _destinations.GetOrAdd(key, _initer).Value;

            try
            {
                await block.InputBlock.SendAsync(input, _token);
                var output = await block.OutputBlock.ReceiveAsync(item => item.Equals(input), _token).Timeout();
                await _completedBlock.SendAsync(output, _token);
            }
            catch (Exception e)
            {
                Log?.Errors.Add(e);
            }

            Interlocked.Decrement(ref _parallelCount); 
        }
    }
}