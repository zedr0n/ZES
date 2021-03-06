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
            InvalidateSubscription = new LazySubscription(() =>
                messageQueue.Alerts.OfType<InvalidateProjections>()
                    .Throttle(Configuration.Throttle)
                    .Subscribe(Build.InputBlock.AsObserver()));
        }
    }
}