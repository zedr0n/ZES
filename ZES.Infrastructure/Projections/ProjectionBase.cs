using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using static ZES.Interfaces.Domain.ProjectionStatus;

#pragma warning disable CS4014

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public abstract class ProjectionBase<TState> : IProjection<TState>
        where TState : new()
    {
        private readonly Lazy<Task> _start;
        private readonly IStreamLocator _streamLocator;
        private int _parallel;
        private string _timeline;
        private ActionBlock<Tracked<IEvent>> _updateStateBlock;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectionBase{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store service</param>
        /// <param name="log">Log service</param>
        /// <param name="activeTimeline">Timeline service</param>
        /// <param name="streamLocator">Stream locator</param>
        public ProjectionBase(IEventStore<IAggregate> eventStore, ILog log, ITimeline activeTimeline, IStreamLocator streamLocator)
        {
            EventStore = eventStore;
            Log = log;
            ActiveTimeline = activeTimeline;
            _streamLocator = streamLocator;
            CancellationSource = new RepeatableCancellationTokenSource();
            _start = new Lazy<Task>(() => Task.Run(Start));
            var options = new DataflowOptions { RecommendedParallelismIfMultiThreaded = 1 };

            Build = new BuildFlow(options, this);

            StatusSubject.Where(s => s != Sleeping)
                .Subscribe(s => Log?.Info($"[{Timeline}]{GetType().GetFriendlyName()} : {s.ToString()}"));
        }

        /// <inheritdoc />
        public IObservable<ProjectionStatus> Ready
        {
            get
            {
                var unused = _start.Value;
                return StatusSubject.AsObservable()
                    .Timeout(Configuration.Timeout)
                    .FirstAsync(s => s == Listening);
            }
        }

        /// <inheritdoc />
        public Guid Guid { get; } = Guid.NewGuid();

        /// <inheritdoc/>
        public string Timeline
        {
            get => _timeline ?? ActiveTimeline.Id;
            set => _timeline = value;
        }

        /// <inheritdoc />
        public virtual Func<string, bool> StreamIdPredicate { get; set; } = s => true;

        /// <inheritdoc />
        public virtual Func<IStream, bool> Predicate { get; set; } = s => true;
        
        /// <inheritdoc />
        public TState State { get; protected set; } = new TState();

        /// <summary>
        /// Gets registered handlers ( State, Event ) -> State
        /// </summary>
        /// <value>
        /// Registered handlers ( State, Event ) -> State
        /// </value>
        public Dictionary<Type, Func<IMessage, TState, TState>> Handlers { get; } =
            new Dictionary<Type, Func<IMessage, TState, TState>>();

        /// <summary>
        /// Gets projection version state
        /// </summary>
        public ConcurrentDictionary<string, int> Versions { get; } = new ConcurrentDictionary<string, int>();
        
        /// <summary>
        /// Gets projection cancellation token
        /// </summary>
        public CancellationToken CancellationToken => CancellationSource.Token;

        /// <summary>
        /// Gets event store 
        /// </summary>
        public IEventStore<IAggregate> EventStore { get; }
        
        /// <summary>
        /// Gets or sets gets log service 
        /// </summary>
        public ILog Log { get; set; }

        /// <summary>
        /// Gets build flow instance
        /// </summary>
        protected BuildFlow Build { get; }

        /// <summary>
        /// Gets projection timestamp
        /// </summary>
        protected virtual long Latest => long.MaxValue;

        /// <summary>
        /// Gets current timeline
        /// </summary>
        protected ITimeline ActiveTimeline { get; }

        /// <summary>
        /// Gets observable representing the projection status
        /// </summary>
        protected BehaviorSubject<ProjectionStatus> StatusSubject { get; } =
            new BehaviorSubject<ProjectionStatus>(Sleeping);

        /// <summary>
        /// Gets current status of the projection
        /// </summary>
        protected ProjectionStatus Status => StatusSubject.AsObservable().FirstAsync().Wait();

        /// <summary>
        /// Gets or sets the <see cref="ZES.Infrastructure.Alerts.InvalidateProjections"/> subscription
        /// </summary>
        protected LazySubscription InvalidateSubscription { get; set; } = new LazySubscription();

        /// <summary>
        /// Gets or sets gets the cancellation source for the  projection
        /// </summary>
        protected RepeatableCancellationTokenSource CancellationSource { get; set; }

        /// <summary>
        /// Projection message processor
        /// </summary>
        /// <param name="e">Message to process</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task When(IEvent e)
        {
            if (CancellationSource.IsCancellationRequested || e == null)
                return Task.CompletedTask;

            var tracked = new Tracked<IEvent>(e);
            Interlocked.Increment(ref _parallel);
            _updateStateBlock.Post(tracked);
            return tracked.Task;
        }

        /// <summary>
        /// Cancel the projection
        /// </summary>
        public void Cancel()
        {
            StatusSubject.OnNext(Cancelling);
            CancellationSource.Cancel();
        }
        
        /// <summary>
        /// Rebuild the projection 
        /// </summary>
        /// <returns>Completes when the projection has finished rebuilding</returns>
        protected virtual async Task Rebuild()
        {
            StatusSubject.OnNext(Building);
            _updateStateBlock = new ActionBlock<Tracked<IEvent>>(UpdateState, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

            CancellationSource.Dispose();
            Versions.Clear();
            lock (State)
            {
                State = new TState();
            }

            var rebuildDispatcher = new ProjectionDispatcher<TState>(Configuration.DataflowOptions, this);
            var liveDispatcher = new ProjectionBufferedDispatcher<TState>(Configuration.DataflowOptions, this);

            CancellationToken.Register(async () =>
            {
                try
                {
                    if (!rebuildDispatcher.CompletionTask.IsFaulted)
                        await rebuildDispatcher.SignalAndWaitForCompletionAsync().Timeout();
                    if (!liveDispatcher.CompletionTask.IsFaulted)
                        await liveDispatcher.SignalAndWaitForCompletionAsync().Timeout();
                }
                catch (Exception e)
                {
                    // StatusSubject.OnNext(Failed);
                    Log?.Errors.Add(e);
                }
                
                StatusSubject.OnNext(Sleeping);
            });
            
            EventStore.Streams
                .TakeWhile(_ => !CancellationSource.IsCancellationRequested)
                .Where(s => s.Timeline == Timeline) 
                .Where(Predicate)
                .Select(s => new Tracked<IStream>(s, CancellationToken))
                .SubscribeOn(Scheduler.Default)
                .Subscribe(liveDispatcher.InputBlock.AsObserver());

            // EventStore.ListStreams(Timeline.Id, StreamIdPredicate, CancellationToken)
            var streams = await _streamLocator.ListStreams(Timeline);
            streams.Where(s => !s.IsSaga && StreamIdPredicate(s.Key)).ToObservable()
                .TakeWhile(_ => !CancellationSource.IsCancellationRequested)
                .Where(Predicate)
                .Select(s => new Tracked<IStream>(s, CancellationToken))
                .SubscribeOn(Scheduler.Default)
                .Subscribe(rebuildDispatcher.InputBlock.AsObserver());

            try
            {
                await rebuildDispatcher.CompletionTask;
                await liveDispatcher.Start();

                if (!CancellationToken.IsCancellationRequested)
                    StatusSubject.OnNext(Listening);
            }
            catch (Exception e)
            {
                StatusSubject.OnNext(Failed);
                Log?.Errors.Add(e);
                CancellationSource.Cancel();
                await Rebuild();
            }
        }
        
        /// <summary>
        /// Starts or restarts the projection
        /// </summary>
        /// <returns>Completes when the projection has  been rebuilt</returns>
        protected async Task Start()
        {
            if (Status != Sleeping)
                return;

            InvalidateSubscription.Start();

            await Rebuild();
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
            Handlers.Add(tEvent, (m, s) => when(m as IEvent, s));
        }

        private void UpdateState(Tracked<IEvent> tracked)
        {
            var e = tracked.Value;
            
            // do not project the future events
            if (Handlers.TryGetValue(e.GetType(), out var handler) && e.Timestamp <= Latest)
            {
                State = handler(e, State);
                Log.Info($"{e.Stream}@{e.Version}:{_parallel}", this);
            }

            Interlocked.Decrement(ref _parallel);
            tracked.Complete();
        }
        
        /// <summary>
        /// Build dataflow
        /// </summary>
        protected class BuildFlow : Dataflow<InvalidateProjections>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="BuildFlow"/> class.
            /// </summary>
            /// <param name="dataflowOptions">Dataflow options</param>
            /// <param name="projection">Related projection</param>
            public BuildFlow(DataflowOptions dataflowOptions, ProjectionBase<TState> projection) 
                : base(dataflowOptions)
            {
                var actionBlock = new ActionBlock<InvalidateProjections>(async e =>
                {
                    projection.Cancel();
                    var status = projection.StatusSubject.AsObservable().Timeout(Configuration.Timeout)
                        .Catch<ProjectionStatus, TimeoutException>(x => Observable.Return(Failed));
                    await status.FirstAsync(s => s == Sleeping || s == Failed);

                    // Task.Factory.StartNew(projection.Rebuild);
                    projection.Rebuild();
                }); 
               
                RegisterChild(actionBlock);
                InputBlock = actionBlock;
            }

            /// <inheritdoc />
            public override ITargetBlock<InvalidateProjections> InputBlock { get; }
        }
    }
}
