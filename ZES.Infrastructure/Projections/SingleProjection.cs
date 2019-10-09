using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Dataflow;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using static ZES.Interfaces.Domain.ProjectionStatus;

namespace ZES.Infrastructure.Projections
{
    /// <summary>
    /// Single stream type projection
    /// </summary>
    /// <typeparam name="TState">Projection state type</typeparam>
    public class SingleProjection<TState> : ProjectionBase<TState>
        where TState : class, new()
    {
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ILog _log;
        private readonly ITimeline _timeline;

        private ITargetBlock<IMessage> _inputBlock;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleProjection{TState}"/> class.
        /// </summary>
        public SingleProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline)
        {
            _eventStore = eventStore;
            _log = log;
            _timeline = timeline;
            CancellationSource = new RepeatableCancellationTokenSource(() => new CancellationTokenSource());
            StatusSubject.Where(s => s != Sleeping).Subscribe(s => _log?.Info($"{GetType().GetFriendlyName()} : {s.ToString()}" ));

            InvalidateSubscription = new LazySubscription(() => 
                messageQueue.Alerts.OfType<InvalidateProjections>().Subscribe(async s => await Rebuild()));
            _inputBlock = new ActionBlock<IMessage>(m => When(m));
        }

        /// <inheritdoc />
        protected override long Now => _timeline.Now;

        /// <inheritdoc />
        protected override async Task Rebuild()
        {
            if (Status == Cancelling)
                return;

            CancellationSource.Cancel();
            
            var status = await StatusSubject.AsObservable().Timeout(Configuration.Timeout)
                .Where(s => s == Sleeping || s == Failed).FirstAsync()
                .Catch<ProjectionStatus, TimeoutException>(e => Observable.Return(Failed));

            if (status == Failed)
                return;
            
            StatusSubject.OnNext(Building);
            CancellationSource.Dispose();
            
            lock (State)
                State = new TState();

            var options = new DataflowOptionsEx
            {
                RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance,
                FlowMonitorEnabled = false
            };
            
            var rebuildDispatcher = new Dispatcher(options, this);
            var liveDispatcher = new BufferedDispatcher(options, this);

            _eventStore.Streams
                .TakeWhile(_ => !CancellationSource.IsCancellationRequested)
                .Select(s => new Tracked<IStream>(s)).Subscribe(liveDispatcher.InputBlock.AsObserver());
            
            _eventStore.ListStreams(_timeline.Id)
                .TakeWhile(_ => !CancellationSource.IsCancellationRequested)
                .Select(s => new Tracked<IStream>(s))
                .Finally(() =>
                {
                    if (!CancellationSource.IsCancellationRequested)
                        liveDispatcher.Start();
                }) 
                .Subscribe(rebuildDispatcher.InputBlock.AsObserver());
            
            CancellationToken.Register(async () =>
            {
                StatusSubject.OnNext(Cancelling);
                await rebuildDispatcher.SignalAndWaitForCompletionAsync();
                await liveDispatcher.SignalAndWaitForCompletionAsync();
                StatusSubject.OnNext(Sleeping);
            });

            try
            {
                await rebuildDispatcher.CompletionTask.Timeout();
            }
            catch (Exception e)
            {
                StatusSubject.OnNext(Failed);
                _log.Errors.Add(e);
                CancellationSource.Cancel();
            }
            
            _log?.Debug($"Rebuild finished!", this); 
            StatusSubject.OnNext(Listening);
        }

        private class Dispatcher : ParallelDataDispatcher<string, Tracked<IStream>>
        {
            private readonly SingleProjection<TState> _projection;
            private readonly Dataflow<Tracked<IStream>, Tracked<IStream>> _buffer;

            public Dispatcher(DataflowOptions options, SingleProjection<TState> projection) 
                : base(s => s.Value.Key, options, projection.CancellationToken)
            {
                Log = projection._log;
                _projection = projection;
                
                _buffer = new BufferBlock<Tracked<IStream>>().ToDataflow();
                projection.CancellationToken.Register(() =>
                    _buffer.LinkTo(DataflowBlock.NullTarget<Tracked<IStream>>().ToDataflow()));
            }

            public void Start() => _buffer.LinkTo(InputBlock.ToDataflow());

            protected override Dataflow<Tracked<IStream>> CreateChildFlow(string dispatchKey) => new Flow(m_dataflowOptions, _projection);
        }

        private class BufferedDispatcher : Dataflow<Tracked<IStream>>
        {
            private readonly Dataflow<Tracked<IStream>, Tracked<IStream>> _buffer;
            private readonly Dispatcher _dispatcher;

            public BufferedDispatcher(DataflowOptions dataflowOptions, SingleProjection<TState> projection) 
                : base(dataflowOptions)
            {
                _buffer = new BufferBlock<Tracked<IStream>>().ToDataflow();
                _dispatcher = new Dispatcher(dataflowOptions, projection);

                projection.CancellationToken.Register(() =>
                    _buffer.LinkTo(DataflowBlock.NullTarget<Tracked<IStream>>().ToDataflow()));
                
                RegisterChild(_buffer);
                RegisterChild(_dispatcher);
            }

            public override ITargetBlock<Tracked<IStream>> InputBlock => _buffer.InputBlock;
            
            public void Start() => _buffer.LinkTo(_dispatcher);
        }

        private class Flow : Dataflow<Tracked<IStream>>
        {
            private readonly SingleProjection<TState> _projection;
            private readonly IEventStore<IAggregate> _eventStore;
            private readonly ILog _log;
            private readonly CancellationToken _token;
            
            private int _version = ExpectedVersion.EmptyStream;

            public Flow(DataflowOptions dataflowOptions, SingleProjection<TState> projection)
                : base(dataflowOptions)
            {
                _projection = projection;
                _eventStore = projection._eventStore;
                _log = projection._log;
                _token = projection.CancellationToken;
                
                var block = new ActionBlock<Tracked<IStream>>(Process);
                
                RegisterChild(block);
                InputBlock = block;
            }

            public override ITargetBlock<Tracked<IStream>> InputBlock { get; }

            private async Task Process(Tracked<IStream> s)
            {
                var stream = s.Value;
                _log?.Debug($"{stream.Key}@{stream.Version} <- {_version}", $"{Parents.Select(p => p.Name).Aggregate((a, n) => a + n)}->{Name}");

                if (stream.Version <= ExpectedVersion.EmptyStream)
                    stream.Version = 0;

                if (_version > stream.Version) 
                    _log?.Warn($"Stream update is version {stream.Version}, behind projection version {_version}", GetDetailedName());

                if (_version < stream.Version)
                {
                    await _eventStore.ReadStream<IEvent>(stream, _version + 1)
                        .TakeWhile(_ => !_token.IsCancellationRequested)
                        .Do(async e =>
                        {
                            await _projection._inputBlock.SendAsync(e, _token);
                            _version++;
                        })
                        .LastOrDefaultAsync();
                }
                    
                s.Complete();    
            }
        }
    }
}