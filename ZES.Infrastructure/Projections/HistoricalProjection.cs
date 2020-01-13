using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    /// <summary>
    /// Historical projection decorator
    /// </summary>
    /// <typeparam name="TState">Projection state</typeparam>
    public class HistoricalProjection<TState> : SingleProjection<TState>, IHistoricalProjection
        where TState : new()
    {
        private long _timestamp;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoricalProjection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Aggregate event store</param>
        /// <param name="log">Application log</param>
        /// <param name="iProjection">Original projection</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="timeline">Active branch</param>
        public HistoricalProjection(
            IEventStore<IAggregate> eventStore,
            ILog log,
            IProjection<TState> iProjection,
            IMessageQueue messageQueue,
            ITimeline timeline)
            : base(eventStore, log, timeline, messageQueue)
        {
            var projection = (ProjectionBase<TState>)iProjection;
            foreach (var h in projection.Handlers)
                Register(h.Key, (e, s) => h.Value(e, s));
            
            InvalidateSubscription = new LazySubscription();
        }

        /// <inheritdoc />
        protected override long Latest => _timestamp;

        /// <inheritdoc />
        public async Task Init(long timestamp)
        {
            Log.Trace(string.Empty, this);
            _timestamp = timestamp;
            await Start();
        }
    }
}