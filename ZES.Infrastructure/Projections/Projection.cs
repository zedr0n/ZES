using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

using static ZES.Interfaces.Domain.ProjectionStatus;

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

        private readonly BehaviorSubject<ProjectionStatus> _statusSubject = new BehaviorSubject<ProjectionStatus>(Sleeping);
        
        private CancellationTokenSource _cancellationSource;

        private int _build = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Projection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Aggregate event store</param>
        /// <param name="log">Application log</param>
        /// <param name="messageQueue">Application message queue</param>
        /// <param name="timeline">Active branch tracker</param>
        /// <param name="streamDispatcher">Dispatcher fluent builder</param>
        protected Projection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline, ProjectionDispatcher.Builder streamDispatcher)
        {
            _eventStore = eventStore;
            Log = log;
            _messageQueue = messageQueue;
            _streamDispatcher = streamDispatcher;
            _timeline = timeline;

            _statusSubject.Subscribe(s => Log?.Info($"{GetType().GetFriendlyName()} : {s.ToString()}" ));
            OnInit();
        }

        /// <inheritdoc />
        public Task Complete => _statusSubject.AsObservable().Timeout(Configuration.Timeout).FirstAsync(s => s == Listening).ToTask();

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
        public virtual string Key(IStream stream) => stream.Key;

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
            Interlocked.Increment(ref _build);
            _statusSubject.OnNext(Building);
            
            _cancellationSource?.Cancel();
            _cancellationSource = new CancellationTokenSource();

            lock (State)
                State = new TState();

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
                .Bind(this)
                .OnError(async t => await Rebuild());

            var liveDispatcher = _streamDispatcher
                .WithCancellation(_cancellationSource)
                .WithOptions(new DataflowOptionsEx
                {
                    RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance > 1
                        ? 2 * Configuration.ThreadsPerInstance
                        : 1,
                    FlowMonitorEnabled = false
                })
                .DelayUntil(new Lazy<Task>(() => rebuildDispatcher.CompletionTask))
                .Bind(this)
                .OnError(async t => await Rebuild());

            _eventStore.ListStreams(_timeline.Id).Subscribe(rebuildDispatcher.InputBlock.AsObserver(), _cancellationSource.Token);
            _eventStore.Streams.Subscribe(liveDispatcher.InputBlock.AsObserver(), _cancellationSource.Token);

            var task = rebuildDispatcher.CompletionTask;
            try
            {
                await task;
            }
            catch (Exception)
            {
                // ignored
            }

            Interlocked.Decrement(ref _build);
            if (task.IsFaulted)
            {
                Log?.Trace("Rebuild faulted", this);
            }
            else if (task.IsCanceled)
            {
                Log?.Trace("Rebuild cancelled", this);
            }
            else
            {
                _statusSubject.OnNext(_build == 0 ? Listening : Building);
            }
        }

        private void When(IEvent e)
        {
            Log.Trace($"Stream {e?.Stream}@{e?.Version}", this);
            if (_cancellationSource.IsCancellationRequested)
                throw new InvalidOperationException();

            if (e == null)
                return;
            
            if (!Handlers.TryGetValue(e.GetType(), out var handler))
                return;
            
            State = handler(e, State);
        }
    }
}