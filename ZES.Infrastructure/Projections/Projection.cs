using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
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
    /// <inheritdoc cref="IProjection{TState}" />
    public abstract partial class Projection<TState> : ProjectionBase<TState>
        where TState : new()
    {
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IMessageQueue _messageQueue;
        private readonly ITimeline _timeline;

        private readonly Dispatcher.Builder _streamDispatcher;

        private readonly ConcurrentDictionary<string, int> _versions = new ConcurrentDictionary<string, int>(); 

        private CancellationTokenSource _cancellationSource;

        private int _build;

        /// <summary>
        /// Initializes a new instance of the <see cref="Projection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Aggregate event store</param>
        /// <param name="log">Application log</param>
        /// <param name="messageQueue">Application message queue</param>
        /// <param name="timeline">Active branch tracker</param>
        /// <param name="streamDispatcher">Dispatcher fluent builder</param>
        protected Projection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline, Dispatcher.Builder streamDispatcher)
        {
            _eventStore = eventStore;
            _messageQueue = messageQueue;
            Log = log;
            _streamDispatcher = streamDispatcher;
            _timeline = timeline;

            StatusSubject.Where(s => s != Sleeping).Subscribe(s => Log?.Info($"{GetType().GetFriendlyName()} : {s.ToString()}" ));

            OnInit();
        }
        
        internal ILog Log { get; }

        /// <inheritdoc />
        protected override long Now => _timeline.Now;

        protected virtual void OnInit()
        {
            InvalidateSubscription = new LazySubscription(
                () => _messageQueue.Alerts.OfType<InvalidateProjections>().Subscribe(async s => await Rebuild()));
        }

        /// <inheritdoc />
        protected override async Task Rebuild()
        {
            Log?.Debug($"Rebuild : {_build}", this);
            var status = await StatusSubject.AsObservable().FirstAsync();

            if (status == Cancelling)
                return;
            
            Interlocked.Increment(ref _build);

            _cancellationSource?.Cancel();
            status = await StatusSubject.AsObservable().Timeout(Configuration.Timeout)
                .Where(s => s == Sleeping || s == Failed).FirstAsync()
                .Catch<ProjectionStatus, TimeoutException>(e => Observable.Return(Failed));

            if (status == Failed)
            {
                Interlocked.Decrement(ref _build);
                return;
            }

            _cancellationSource = new CancellationTokenSource();
            _versions.Clear();

            StatusSubject.OnNext(Building);

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
                .Bind(this);

            var liveDispatcher = _streamDispatcher
                .WithCancellation(_cancellationSource)
                .WithOptions(new DataflowOptionsEx
                {
                    RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance > 1
                        ? 2 * Configuration.ThreadsPerInstance
                        : 1,
                    FlowMonitorEnabled = false
                })
                .DelayUntil(rebuildDispatcher.CompletionTask)
                .Bind(this)
                .OnError(async () => await Rebuild());

            _cancellationSource.Token.Register(async () =>
            {
                StatusSubject.OnNext(Cancelling);
                
                try
                {
                    liveDispatcher.Complete();
                    if (!liveDispatcher.CompletionTask.IsCompleted)
                        await liveDispatcher.CompletionTask.Timeout();
                    
                    rebuildDispatcher.Complete();
                    if (!rebuildDispatcher.CompletionTask.IsCompleted)
                        await rebuildDispatcher.CompletionTask.Timeout();
                    
                    StatusSubject.OnNext(Sleeping);
                }
                catch (Exception e)
                {
                    StatusSubject.OnNext(Failed);
                    Log.Errors.Add(e);
                    StatusSubject.OnNext(Sleeping);
                    await Rebuild();
                }
            });

            _eventStore.ListStreams(_timeline.Id)
                .Finally(() => _eventStore.Streams.Subscribe(liveDispatcher.InputBlock.AsObserver(), _cancellationSource.Token))
                .Subscribe(rebuildDispatcher.InputBlock.AsObserver(), _cancellationSource.Token);

            try
            {
                await rebuildDispatcher.CompletionTask.Timeout();

                if (_build == 1 && !_cancellationSource.IsCancellationRequested)
                    StatusSubject.OnNext(Listening);
            }
            catch (Exception e)
            {
                StatusSubject.OnNext(Failed);
                Log.Errors.Add(e);
                _cancellationSource.Cancel();
            }
            finally
            {
                Interlocked.Decrement(ref _build); 
                Log?.Debug($"Rebuild finished : {_build}", this); 
            }
        }
    }
}