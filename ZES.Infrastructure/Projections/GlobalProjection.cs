using System;
using System.Reactive.Linq;
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
    /// <summary>
    /// Global projection
    /// </summary>
    /// <typeparam name="TState">Projection state type</typeparam>
    public class GlobalProjection<TState> : ProjectionBase<TState>
        where TState : new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalProjection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store service</param>
        /// <param name="log">Log service</param>
        /// <param name="timeline">Timeline service</param>
        /// <param name="messageQueue">Message queue service</param>
        public GlobalProjection(IEventStore<IAggregate> eventStore, ILog log, ITimeline timeline, IMessageQueue messageQueue)
            : base(eventStore, log, timeline)
        {
            InvalidateSubscription = new LazySubscription(() =>
                messageQueue.Alerts.OfType<InvalidateProjections>()
                    .Subscribe(Build.InputBlock.AsObserver()));
        }

        /// <inheritdoc />
        protected override async Task Rebuild()
        {
            StatusSubject.OnNext(Building);
            
            CancellationSource.Dispose();
            Versions.Clear();
            lock (State)
            {
                State = new TState();
            }

            var options = new DataflowOptionsEx
            {
                RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance,
                FlowMonitorEnabled = false
            };

            var rebuildDispatcher = new ProjectionDispatcher<TState>(options, this);
            var liveDispatcher = new ProjectionBufferedDispatcher<TState>(options, this);

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
                try
                {
                    if (!rebuildDispatcher.CompletionTask.IsFaulted)
                        await rebuildDispatcher.SignalAndWaitForCompletionAsync().Timeout();
                    if (!liveDispatcher.CompletionTask.IsFaulted)
                        await liveDispatcher.SignalAndWaitForCompletionAsync().Timeout();
                    Log?.Info("Dispatchers cancelled");
                    StatusSubject.OnNext(Sleeping);
                }
                catch (Exception e)
                {
                    // StatusSubject.OnNext(Failed);
                    Log?.Errors.Add(e);
                    StatusSubject.OnNext(Sleeping);
                }
            });

            try
            {
                await rebuildDispatcher.CompletionTask.Timeout();
                await liveDispatcher.Start();
                StatusSubject.OnNext(Listening);
            }
            catch (Exception e)
            {
                StatusSubject.OnNext(Failed);
                Log?.Errors.Add(e);
                CancellationSource.Cancel();
                StatusSubject.OnNext(Sleeping);
                await Rebuild();
            }
        }
    }
}