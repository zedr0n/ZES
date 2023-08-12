using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

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
        /// <param name="activeTimeline">Timeline service</param>
        /// <param name="messageQueue">Message queue service</param>
        /// <param name="streamLocator">Stream locator</param>
        public GlobalProjection(IEventStore<IAggregate> eventStore, ILog log, ITimeline activeTimeline, IMessageQueue messageQueue, IStreamLocator streamLocator)
            : base(eventStore, log, activeTimeline, streamLocator)
        {
            var gate = messageQueue.RetroactiveExecution.DistinctUntilChanged()
                .Throttle(x => Observable.Timer(TimeSpan.FromMilliseconds(x ? 0 : 10)))
                .StartWith(false)
                .DistinctUntilChanged();

            InvalidateSubscription = new LazySubscription(() =>
                messageQueue.Alerts.OfType<InvalidateProjections>()
                    .Window(gate)
                    .Select((w, i) => i % 2 == 0 ? w.ToList().SelectMany(x => x).TakeLast(1) : w)
                    .Concat()
                    .Subscribe(Build.InputBlock.AsObserver()));

            ImmediateInvalidateSubscription = new LazySubscription(() =>
                messageQueue.Alerts.OfType<ImmediateInvalidateProjections>()
                    .Select(x => new InvalidateProjections())
                    .Subscribe(Build.InputBlock.AsObserver()));
        }
    }
}