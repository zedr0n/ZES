using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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
    /// <inheritdoc cref="IProjection{TState}" />
    public abstract partial class Projection<TState> : IProjection<TState>
        where TState : new()
    {
        private readonly IMessageQueue _messageQueue;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ITimeline _timeline;

        private readonly ProjectionDispatcher.Builder _streamDispatcher;

        private CancellationTokenSource _cancellationSource;
        private TaskCompletionSource<IStream> _taskCompletion = new TaskCompletionSource<IStream>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Projection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Aggregate event store</param>
        /// <param name="log">Application log</param>
        /// <param name="messageQueue">Application message queue</param>
        /// <param name="timeline"></param>
        /// <param name="streamDispatcher"></param>
        protected Projection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline, ProjectionDispatcher.Builder streamDispatcher)
        {
            _eventStore = eventStore;
            Log = log;
            _messageQueue = messageQueue;
            _streamDispatcher = streamDispatcher;
            _timeline = timeline;

            OnInit();
        }

        /// <inheritdoc />
        public Task Complete => Observable.FromAsync(async () => await _taskCompletion.Task)
            .Timeout(Configuration.Timeout == TimeSpan.FromMilliseconds(-1) ? TimeSpan.MaxValue : Configuration.Timeout).ToTask(); 

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

        /// <inheritdoc />
        public string Key(IStream stream) => stream.Key;

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

        private async Task Rebuild()
        {
            Log.Trace("Rebuild started", this);

            _cancellationSource?.Cancel();
            _cancellationSource = new CancellationTokenSource();

            lock (State)
                State = new TState();
            
            _taskCompletion = new TaskCompletionSource<IStream>();

            var rebuildDispatcher = _streamDispatcher
                .WithCancellation(_cancellationSource)
                .WithOptions(new DataflowOptionsEx
                {
                    RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance > 1
                        ? 2 * Configuration.ThreadsPerInstance
                        : 1,
                    MonitorInterval = TimeSpan.FromMilliseconds(1000),
                    FlowMonitorEnabled = false
                    /*FlowMonitorEnabled = true,
                    BlockMonitorEnabled = true,
                    PerformanceMonitorMode = DataflowOptions.PerformanceLogMode.Verbose*/
                }) // Timeout = TimeSpan.FromMilliseconds(1000) } )
                .Bind(this);
                //.OnError(async t => await Rebuild());

            var liveDispatcher = _streamDispatcher
                .WithCancellation(_cancellationSource)
                .WithOptions(new DataflowOptionsEx
                {
                    RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance > 1
                        ? 2 * Configuration.ThreadsPerInstance
                        : 1,
                    FlowMonitorEnabled = false
                })
                .DelayUntil(new Lazy<Task>(() => Complete))
                .Bind(this)
                .OnError(async t => await Rebuild());

            rebuildDispatcher.CompletionTask.ContinueWith(t => _taskCompletion.TrySetResult(null));

            _eventStore.ListStreams(_timeline.Id).Subscribe(rebuildDispatcher.InputBlock.AsObserver(), _cancellationSource.Token);
            _eventStore.Streams.Subscribe(liveDispatcher.InputBlock.AsObserver(), _cancellationSource.Token);

            await Complete;
            //await Observable.FromAsync(async () => await Complete).Timeout(Configuration.Timeout); 
        }

        private void When(IEvent e)
        {
            Log.Trace($"Stream {e?.Stream}@{e?.Version}", this);
            if (_cancellationSource.IsCancellationRequested)
            {
                throw new InvalidOperationException();
                Log.Error("Cancellation requested!");
                return;
            }

            if (e == null)
                return;
            
            if (!Handlers.TryGetValue(e.GetType(), out var handler))
                return;
            
            State = handler(e, State);
        }
    }
}