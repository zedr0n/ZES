using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using static ZES.Interfaces.Domain.ProjectionStatus;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public abstract class ProjectionBase<TState> : IProjection<TState>
        where TState : new()
    {
        protected ITargetBlock<Tracked<IEvent>> InputBlock { get; }
        protected IEventStore<IAggregate> EventStore { get; }
        protected ITimeline Timeline { get; }
        protected ILog Log;

        private int _parallel = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectionBase{TState}"/> class.
        /// </summary>
        public ProjectionBase(IEventStore<IAggregate> eventStore, ILog log, ITimeline timeline)
        {
            EventStore = eventStore;
            Log = log;
            Timeline = timeline;
            CancellationSource = new RepeatableCancellationTokenSource();
            InputBlock = new ActionBlock<Tracked<IEvent>>(m => When(m), new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Configuration.ThreadsPerInstance
            });

            StatusSubject.Where(s => s != Sleeping)
                .Subscribe(s => Log?.Info($"{GetType().GetFriendlyName()} : {s.ToString()}"));
        }

        /// <inheritdoc />
        public IObservable<ProjectionStatus> Ready
        {
            get
            {
                Task.Run(Start);
                return StatusSubject.AsObservable()
                    .Timeout(Configuration.Timeout)
                    .FirstAsync(s => s == Listening);
            }
        }

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
        /// Gets projection timestamp
        /// </summary>
        protected virtual long Now => Timeline.Now;

        /// <inheritdoc />
        public virtual string Key(IStream stream) => stream.Key;

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
        /// Gets projection cancellation token
        /// </summary>
        protected CancellationToken CancellationToken => CancellationSource.Token;

        /// <summary>
        /// Rebuild the projection 
        /// </summary>
        /// <returns>Completes when the projection has finished rebuilding</returns>
        protected virtual Task Rebuild() => Task.CompletedTask;

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


        /// <summary>
        /// Projection message processor
        /// </summary>
        /// <param name="e">Message to process</param>
        protected void When(IEvent e)
        {
            if (CancellationSource.IsCancellationRequested || e == null)
                return;

            // Log.Trace($"Stream {e.Stream}@{e.Version}", this);
            if (!Handlers.TryGetValue(e.GetType(), out var handler))
                return;

            // do not project the future events
            if (e.Timestamp > Now)
                return;
            Interlocked.Increment(ref _parallel);

            State = handler(e, State);
            Log.Debug($"{e.Stream}@{e.Version}:{_parallel}", this);
            Interlocked.Decrement(ref _parallel);
        }
        
        /// <summary>
        /// Projection message processor
        /// </summary>
        /// <param name="e">Message to process</param>
        protected void When(Tracked<IEvent> e)
        {
            var @event = e.Value;
            Interlocked.Increment(ref _parallel);
            
            // do not project the future events
            if ( @event == null 
                 || CancellationSource.IsCancellationRequested
                 || !Handlers.TryGetValue(@event.GetType(), out var handler) 
                 || @event.Timestamp > Now)
            {
                e.Complete();
                Interlocked.Decrement(ref _parallel);
                return;
            }

            State = handler(@event, State);
            e.Complete();
            Log.Debug($"{@event.Stream}@{@event.Version}:{_parallel}", this);
            Interlocked.Decrement(ref _parallel);
        }

    }
}
