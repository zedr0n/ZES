using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
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
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectionBase{TState}"/> class.
        /// </summary>
        public ProjectionBase()
        {
            CancellationSource = new RepeatableCancellationTokenSource();
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
        public Dictionary<Type, Func<IMessage, TState, TState>> Handlers { get; } = new Dictionary<Type, Func<IMessage, TState, TState>>();
        
        /// <summary>
        /// Gets projection timestamp
        /// </summary>
        protected virtual long Now { get; } = 0;

        /// <inheritdoc />
        public virtual string Key(IStream stream) => stream.Key;

        /// <summary>
        /// Gets observable representing the projection status
        /// </summary>
        protected BehaviorSubject<ProjectionStatus> StatusSubject { get; } = new BehaviorSubject<ProjectionStatus>(Sleeping);

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
        protected void When(IMessage e)
        {
            if (CancellationSource.IsCancellationRequested || e == null)
                return;    
            
            // Log.Trace($"Stream {e.Stream}@{e.Version}", this);
            if (!Handlers.TryGetValue(e.GetType(), out var handler))
                return;

            // do not project the future events
            if (e.Timestamp > Now)
                return;
            
            State = handler(e, State);
        }
    }
}