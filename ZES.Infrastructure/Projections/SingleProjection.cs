using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        where TState : new()
    {
        private readonly ConcurrentDictionary<string, int> _versions = new ConcurrentDictionary<string, int>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleProjection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store service</param>
        /// <param name="log">Log service</param>
        /// <param name="timeline">Timeline service</param>
        /// <param name="messageQueue">Message queue service</param>
        public SingleProjection(IEventStore<IAggregate> eventStore, ILog log, ITimeline timeline, IMessageQueue messageQueue)
            : base(eventStore, log, timeline)
        {
            InvalidateSubscription = new LazySubscription(() =>
                messageQueue.Alerts.OfType<InvalidateProjections>().Subscribe(async s => await Rebuild()));
        }

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
            _versions.Clear();
            lock (State)
            {
                State = new TState();
            }

            var options = new DataflowOptionsEx
            {
                RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance,
                FlowMonitorEnabled = false
            };

            var rebuildDispatcher = new Dispatcher(options, this);
            var liveDispatcher = new BufferedDispatcher(options, this);

            EventStore.Streams
                .TakeWhile(_ => !CancellationSource.IsCancellationRequested)
                .Select(s => new Tracked<IStream>(s))
                .Subscribe(liveDispatcher.InputBlock.AsObserver());

            EventStore.ListStreams(Timeline.Id)
                .TakeWhile(_ => !CancellationSource.IsCancellationRequested)
                .Select(s => new Tracked<IStream>(s))
                .Subscribe(rebuildDispatcher.InputBlock.AsObserver());

            CancellationToken.Register(async () =>
            {
                if (Status != Failed)
                    StatusSubject.OnNext(Cancelling);
                try
                {
                    if (!rebuildDispatcher.CompletionTask.IsFaulted)
                        await rebuildDispatcher.SignalAndWaitForCompletionAsync();
                    if (!liveDispatcher.CompletionTask.IsFaulted)
                        await liveDispatcher.SignalAndWaitForCompletionAsync();
                    StatusSubject.OnNext(Sleeping);
                }
                catch (Exception e)
                {
                    StatusSubject.OnNext(Failed);
                    Log.Errors.Add(e);
                    StatusSubject.OnNext(Sleeping);
                }
            });

            try
            {
                await rebuildDispatcher.CompletionTask.Timeout();
                await liveDispatcher.Start();
                StatusSubject.OnNext(Listening);
                Log?.Info($"Rebuild finished!", this);
            }
            catch (Exception e)
            {
                StatusSubject.OnNext(Failed);
                Log?.Errors.Add(e);
                CancellationSource.Cancel();
            }
        }

        private class Dispatcher : ParallelDataDispatcher<string, Tracked<IStream>>
        {
            private readonly SingleProjection<TState> _projection;

            public Dispatcher(DataflowOptions options, SingleProjection<TState> projection) 
                : base(s => s.Value.Key, options, projection.CancellationToken, projection.GetType())
            {
                _projection = projection;
                Log = projection.Log;
                
                CompletionTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Errors.Add(t.Exception);
                });
            }

            protected override Dataflow<Tracked<IStream>> CreateChildFlow(string dispatchKey) => new Flow(m_dataflowOptions, _projection);
        }

        private class BufferedDispatcher : Dataflow<Tracked<IStream>>
        {
            private readonly Dataflow<Tracked<IStream>, Tracked<IStream>> _buffer;
            private readonly Dispatcher _dispatcher;
            private readonly ILog _log;

            public BufferedDispatcher(DataflowOptions dataflowOptions, SingleProjection<TState> projection) 
                : base(dataflowOptions)
            {
                _buffer = new BufferBlock<Tracked<IStream>>().ToDataflow();
                _dispatcher = new Dispatcher(dataflowOptions, projection);
                _log = projection.Log;

                projection.CancellationToken.Register(() =>
                    _buffer.LinkTo(DataflowBlock.NullTarget<Tracked<IStream>>().ToDataflow()));
                
                RegisterChild(_buffer);
                RegisterChild(_dispatcher);
            }

            public override ITargetBlock<Tracked<IStream>> InputBlock => _buffer.InputBlock;
            
            public async Task Start()
            {
                var task = Task.CompletedTask;
                var count = _buffer.BufferedCount;
                _log.Info($"{count} streams in buffer", this);
                
                var awaiter = new ActionBlock<Tracked<IStream>>(
                    async s =>
                {
                    _log.Info($"{s.Value.Key}@{s.Value.Version}");
                    await s.Task;
                    Interlocked.Decrement(ref count);
                    if (count <= 0)
                        Complete();
                }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Configuration.ThreadsPerInstance }).ToDataflow();
                
                // RegisterChild(awaiter);
                _buffer.LinkTo(_dispatcher);
                await Task.CompletedTask;

                // _buffer.LinkToMultiple(awaiter, _dispatcher);

                // await awaiter.CompletionTask;
            }
        }

        private class Flow : Dataflow<Tracked<IStream>>
        {
            private readonly SingleProjection<TState> _projection;
            private readonly IEventStore<IAggregate> _eventStore;
            private readonly ILog _log;
            private readonly CancellationToken _token;
            
            private readonly ConcurrentDictionary<string, int> _versions;
            
            public Flow(DataflowOptions dataflowOptions, SingleProjection<TState> projection)
                : base(dataflowOptions)
            {
                _projection = projection;
                _eventStore = projection.EventStore;
                _log = projection.Log;
                _token = projection.CancellationToken;
                _versions = projection._versions;
                
                var block = new ActionBlock<Tracked<IStream>>(Process);
                RegisterChild(block);
                InputBlock = block;
            }

            /// <inheritdoc />
            public override ITargetBlock<Tracked<IStream>> InputBlock { get; }

            private async Task Process(Tracked<IStream> trackedStream)
            {
                var s = trackedStream.Value;
                var version = _versions.GetOrAdd(s.Key, ExpectedVersion.EmptyStream);

                if (s.Version <= ExpectedVersion.EmptyStream)
                    s.Version = 0;

                if (version > s.Version) 
                    _log?.Warn($"Stream update is version {s.Version}, behind projection version {version}", GetDetailedName());

                if (version < s.Version)
                {
                    _log?.Debug($"{s.Key}@{s.Version} <- {version}", $"{Parents.Select(p => p.Name).Aggregate((a, n) => a + n)}->{Name}");
                    
                    var origVersion = version;
                    await _eventStore.ReadStream<IEvent>(s, version + 1)
                        .TakeWhile(_ => !_token.IsCancellationRequested)
                        .Select(e =>
                        {
                            _projection.When(e);
                            version++;
                            return true;
                        })
                        .Timeout(Configuration.Timeout)
                        .LastOrDefaultAsync();
                    
                    if (!_versions.TryUpdate(s.Key, version, origVersion))
                        throw new InvalidOperationException("Failed updating concurrent versions of projections");
                }
                
                trackedStream.Complete();
            }
        }
    }
}