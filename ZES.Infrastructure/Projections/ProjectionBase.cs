using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Dataflow;
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
        private readonly ITargetBlock<IMessage> _inputBlock;
        
        private readonly ConcurrentDictionary<string, int> _versions = new ConcurrentDictionary<string, int>();

        protected IEventStore<IAggregate> EventStore { get; }
        protected ITimeline Timeline { get; }
        protected ILog Log;

        
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectionBase{TState}"/> class.
        /// </summary>
        public ProjectionBase(IEventStore<IAggregate> eventStore, ILog log, ITimeline timeline)
        {
            EventStore = eventStore;
            Log = log;
            Timeline = timeline;
            CancellationSource = new RepeatableCancellationTokenSource();
            _inputBlock = new ActionBlock<IMessage>(m => When(m));
            
            StatusSubject.Where(s => s != Sleeping).Subscribe(s => Log?.Info($"{GetType().GetFriendlyName()} : {s.ToString()}" ));
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
        protected virtual long Now => Timeline.Now;

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
        /// Reset the  projection state
        /// </summary>
        protected void Reset()
        {
            CancellationSource.Dispose();
            _versions.Clear();
            lock (State)
            {
                State = new TState();
            }
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
        
        protected class Dispatcher : ParallelDataDispatcher<string, IStream>
        {
            private readonly ProjectionBase<TState> _projection;

            public Dispatcher(DataflowOptions options, ProjectionBase<TState> projection) 
                : base(s => s.Key, options, projection.CancellationToken)
            {
                _projection = projection;
                Log = projection.Log;
                
                CompletionTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Errors.Add(t.Exception);
                });
            }

            protected override Dataflow<IStream> CreateChildFlow(string dispatchKey) => new Flow(m_dataflowOptions, _projection);
        }

        protected class BufferedDispatcher : Dataflow<IStream>
        {
            private readonly Dataflow<IStream, IStream> _buffer;
            private readonly Dispatcher _dispatcher;

            public BufferedDispatcher(DataflowOptions dataflowOptions, ProjectionBase<TState> projection) 
                : base(dataflowOptions)
            {
                _buffer = new BufferBlock<IStream>().ToDataflow();
                _dispatcher = new Dispatcher(dataflowOptions, projection);

                projection.CancellationToken.Register(() =>
                    _buffer.LinkTo(DataflowBlock.NullTarget<IStream>().ToDataflow()));
                
                RegisterChild(_buffer);
                RegisterChild(_dispatcher);
            }

            public override ITargetBlock<IStream> InputBlock => _buffer.InputBlock;
            
            public void Start() => _buffer.LinkTo(_dispatcher);
        }

        protected class Flow : Dataflow<IStream>
        {
            private readonly ProjectionBase<TState> _projection;
            private readonly IEventStore<IAggregate> _eventStore;
            private readonly ILog _log;
            private readonly CancellationToken _token;
            
            private readonly ConcurrentDictionary<string, int> _versions;
            
            public Flow(DataflowOptions dataflowOptions, ProjectionBase<TState> projection)
                : base(dataflowOptions)
            {
                _projection = projection;
                _eventStore = projection.EventStore;
                _log = projection.Log;
                _token = projection.CancellationToken;
                _versions = projection._versions;
                
                var block = new ActionBlock<IStream>(Process);
                
                RegisterChild(block);
                InputBlock = block;
            }

            public override ITargetBlock<IStream> InputBlock { get; }

            private async Task Process(IStream s)
            {
                var version = _versions.GetOrAdd(s.Key, ExpectedVersion.EmptyStream);

                if (s.Version <= ExpectedVersion.EmptyStream)
                    s.Version = 0;

                if (version > s.Version) 
                    _log?.Warn($"Stream update is version {s.Version}, behind projection version {version}", GetDetailedName());

                if (version >= s.Version)
                    return;
                
                _log?.Debug($"{s.Key}@{s.Version} <- {version}", $"{Parents.Select(p => p.Name).Aggregate((a, n) => a + n)}->{Name}");
                    
                var origVersion = version;
                await _eventStore.ReadStream<IEvent>(s, version + 1)
                    .TakeWhile(_ => !_token.IsCancellationRequested)
                    .Do(async e =>
                    {
                        await _projection._inputBlock.SendAsync(e, _token);
                        version++;
                    })
                    .LastOrDefaultAsync();
                    
                if (!_versions.TryUpdate(s.Key, version, origVersion))
                    throw new InvalidOperationException("Failed updating concurrent versions of projections");
            }
        }
    }
}