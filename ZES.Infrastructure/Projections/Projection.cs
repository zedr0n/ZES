using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public class Projection<TState> : IProjection<TState>
        where TState : new()
    {
        private readonly IMessageQueue _messageQueue;

        private readonly IEventStore<IAggregate> _eventStore;
        private StreamDispatcher _streamDispatcher;

        private CancellationTokenSource _streamSource;
        private CancellationTokenSource _rebuildSource;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Projection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Aggregate event store</param>
        /// <param name="log">Application log</param>
        /// <param name="messageQueue">Application message queue</param>
        protected Projection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue)
        {
            _eventStore = eventStore;
            Log = log;
            _messageQueue = messageQueue;

            _streamSource = new CancellationTokenSource();
            _rebuildSource = new CancellationTokenSource();

            OnInit();
        }

        /// <inheritdoc />
        public Task Complete { get; private set; }

        /// <inheritdoc />
        public TState State { get; protected set; } = new TState();

        /// <summary>
        /// Gets registered handlers ( State, Event ) -> State
        /// </summary>
        /// <value>
        /// Registered handlers ( State, Event ) -> State
        /// </value>
        public Dictionary<Type, Func<IEvent, TState, TState>> Handlers { get; } = new Dictionary<Type, Func<IEvent, TState, TState>>();

        internal ILog Log { get; }

        internal async Task Start(bool rebuild = true)
        {
            if (rebuild)
                await Rebuild();
            else
                ListenToStreams();
            _messageQueue.Alerts.OfType<InvalidateProjections>().Subscribe(async s => await Rebuild());
        }

        internal virtual void OnInit()
        {
            Start();
        }

        /// <summary>
        /// Register the mapping for the event of the type 
        /// </summary>
        /// <param name="when">(State, Event) -> State handler</param>
        /// <typeparam name="TEvent">Event type</typeparam>
        protected void Register<TEvent>(Func<TEvent, TState, TState> when)
            where TEvent : class
        {
            Handlers.Add(typeof(TEvent), (e, s) => when(e as TEvent, s));
        }

        /// <summary>
        /// Register the mapping for the event of the type 
        /// </summary>
        /// <param name="tEvent">Event type</param>
        /// <param name="when">(State, Event) -> State handler</param>
        protected void Register(Type tEvent, Func<IEvent, TState, TState> when)
        {
            Handlers.Add(tEvent, when);
        }

        private void ListenToStreams()
        {
            Log.Trace(string.Empty, this);
            _streamSource.Cancel();
            _streamSource = new CancellationTokenSource();
            
            _streamDispatcher = new StreamDispatcher(new DataflowOptions { RecommendedParallelismIfMultiThreaded = 8 }, When, _streamSource, _eventStore, Log);
            _eventStore.Streams.Subscribe(async s => await _streamDispatcher.SendAsync(s), _streamSource.Token);
        }

        private async Task Rebuild()
        {
            Log.Trace(string.Empty, this);

            _streamSource.Cancel();
            _rebuildSource.Cancel();

            lock (State)
                State = new TState();

            _rebuildSource = new CancellationTokenSource();
            var eventFlow = new EventFlow(When, _rebuildSource);
            await eventFlow.SendAsync(_eventStore.Events);
            Complete = eventFlow.CompletionTask;
            await eventFlow.CompletionTask;
            ListenToStreams();
        }

        private void When(IEvent e)
        {
            if (_rebuildSource.IsCancellationRequested)
            {
                Log.Debug("Cancellation requested!");
                return;
            }

            if (e == null)
                return;

            if (!Handlers.TryGetValue(e.GetType(), out var handler))
                return;
            
            Log.Trace($"Stream {e.Stream}", this);

            State = handler(e, State);
        }

        private class EventFlow : Dataflow<IObservable<IEvent>>
        {
            private readonly BufferBlock<IObservable<IEvent>> _inputBlock;

            public EventFlow(Action<IEvent> when, CancellationTokenSource tokenSource)
                : base(DataflowOptions.Default)
            {
                _inputBlock = new BufferBlock<IObservable<IEvent>>();
                var updateBlock = new ActionBlock<IObservable<IEvent>>(
                    e => e.Finally(_inputBlock.Complete).Subscribe(when, tokenSource.Token),
                    new ExecutionDataflowBlockOptions { CancellationToken = tokenSource.Token });

                _inputBlock.LinkTo(updateBlock, new DataflowLinkOptions { PropagateCompletion = true });

                RegisterChild(_inputBlock);
                RegisterChild(updateBlock);
            }

            public override ITargetBlock<IObservable<IEvent>> InputBlock => _inputBlock;
        }

        private class StreamDispatcher : ParallelDataDispatcher<IStream, string>
        {
            private readonly IEventStore<IAggregate> _store;
            private readonly Action<IEvent> _when;
            private readonly CancellationTokenSource _source;
            private readonly ILog _log;

            private int _parallelCount;
            
            public StreamDispatcher(DataflowOptions option, Action<IEvent> when, CancellationTokenSource source, IEventStore<IAggregate> store, ILog log) 
                : base(s => s.Key, option)
            {
                _when = when;
                _source = source;
                _store = store;
                _log = log;
            }

            protected override Dataflow<IStream> CreateChildFlow(string dispatchKey) =>
                new StreamFlow(_when, _source, _store);

            protected override async Task SendToChild(Dataflow<IStream> dataflow, IStream input)
            {
                Interlocked.Increment(ref _parallelCount);
                _log.Debug($"Projection parallel count : {_parallelCount}");
                
                await ((StreamFlow)dataflow).ProcessAsync(input);
                
                Interlocked.Decrement(ref _parallelCount);
            }
        }

        private class StreamFlow : Dataflow<IStream>
        {
            private readonly BufferBlock<IStream> _inputBlock;
            private int _version = -1;
            private TaskCompletionSource<bool> _next = new TaskCompletionSource<bool>();
            
            public StreamFlow(
                Action<IEvent> when,
                CancellationTokenSource tokenSource,
                IEventStore<IAggregate> eventStore) 
                : base(DataflowOptions.Default)
            {
                _inputBlock = new BufferBlock<IStream>();                
                
                var readBlock = new TransformBlock<IStream, IObservable<IEvent>>(
                    s => eventStore.ReadStream(s, _version + 1),
                    new ExecutionDataflowBlockOptions { CancellationToken = tokenSource.Token }); 
                
                var updateBlock = new ActionBlock<IObservable<IEvent>>(
                    async o =>
                    {
                        o.Subscribe(
                            e =>
                        {
                            when(e);
                            _version = e.Version;
                        }, tokenSource.Token);
                        await o;
                        
                        _next.SetResult(true);
                        _next = new TaskCompletionSource<bool>();
                    },
                    new ExecutionDataflowBlockOptions { CancellationToken = tokenSource.Token, MaxDegreeOfParallelism = 1 });

                _inputBlock.LinkTo(
                    readBlock,
                    new DataflowLinkOptions { PropagateCompletion = true },
                    s => _version < s.Version);
                readBlock.LinkTo(updateBlock);
                
                RegisterChild(_inputBlock);
                RegisterChild(readBlock);
                RegisterChild(updateBlock);
            }

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
            
            /// <summary>
            /// Processes a single event asynchronously by the dataflow 
            /// </summary>
            /// <param name="s">Updated stream</param>
            /// <returns>Task indicating whether the event was processed by the dataflow</returns>
            public async Task<bool> ProcessAsync(IStream s)
            {
                if (!await _inputBlock.SendAsync(s))
                    return false; 
                return await _next.Task;
            }
        }
    }
}