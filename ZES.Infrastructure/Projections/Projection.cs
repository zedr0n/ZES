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
    public partial class Projection<TState> : IProjection<TState>
        where TState : new()
    {
        private readonly IMessageQueue _messageQueue;
        private readonly IEventStore<IAggregate> _eventStore;
        
        private readonly ConcurrentDictionary<string, int> _versions = new ConcurrentDictionary<string, int>();
        
        private Lazy<StreamDispatcher> _streamDispatcher;

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

        internal async Task Start()
        {
            await Rebuild();
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

        private void ListenToStreams(Task start)
        {
            _streamSource.Cancel();
            _streamSource = new CancellationTokenSource();
            
            _streamDispatcher = new Lazy<StreamDispatcher>(() =>
            {
                var dispatcher = new StreamDispatcher(
                    _versions,
                    new DataflowOptionsEx { RecommendedParallelismIfMultiThreaded = 8, Timeout = TimeSpan.FromMilliseconds(1000) },
                    When,
                    _streamSource,
                    _eventStore,
                    Log);

                dispatcher.CompletionTask.ContinueWith(t => Rebuild());
                return dispatcher;
            });
            
            var buffer = new BufferBlock<IStream>();
            start?.ContinueWith(t =>
            {
                Log.Trace("Listening to streams...", this);
                buffer.LinkTo(_streamDispatcher.Value.InputBlock);
            });
            
            _eventStore.Streams.Subscribe(
                async s =>
            {
                try
                {
                    await buffer.SendAsync(s);
                }
                catch (Exception)
                {
                    // ignored
                }
            }, _streamSource.Token);
        }

        private async Task Rebuild()
        {
            Log.Trace("Rebuild started", this);

            _streamSource.Cancel();
            _rebuildSource.Cancel();

            lock (State)
                State = new TState();

            _rebuildSource = new CancellationTokenSource();
            var eventFlow = new EventFlow(When, _rebuildSource);
            await eventFlow.SendAsync(_eventStore.Events);
            
            Complete = eventFlow.CompletionTask;
            
            ListenToStreams(eventFlow.CompletionTask);
            await eventFlow.CompletionTask;
            
            Log.Trace("Rebuild complete", this);
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
            _versions[e.Stream] = e.Version;
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
    }
}