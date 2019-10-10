using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleProjection{TState}"/> class.
        /// </summary>
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
            Reset();

            var options = new DataflowOptionsEx
            {
                RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance,
                FlowMonitorEnabled = false
            };

            var rebuildDispatcher = new Dispatcher(options, this);
            var liveDispatcher = new BufferedDispatcher(options, this);

            EventStore.Streams
                .TakeWhile(_ => !CancellationSource.IsCancellationRequested)
                .Subscribe(liveDispatcher.InputBlock.AsObserver());

            EventStore.ListStreams(Timeline.Id)
                .TakeWhile(_ => !CancellationSource.IsCancellationRequested)
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
                liveDispatcher.Start();
                StatusSubject.OnNext(Listening);
                Log?.Debug($"Rebuild finished!", this);
            }
            catch (Exception e)
            {
                StatusSubject.OnNext(Failed);
                Log?.Errors.Add(e);
                CancellationSource.Cancel();
            }
        }
    }
}