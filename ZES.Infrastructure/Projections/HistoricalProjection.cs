using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    /// <summary>
    /// Historical projection decorator
    /// </summary>
    /// <typeparam name="TState">Projection state</typeparam>
    public sealed class HistoricalProjection<TState> : ProjectionBase<TState>, IHistoricalProjection<TState>
        where TState : new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HistoricalProjection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Aggregate event store</param>
        /// <param name="log">Application log</param>
        /// <param name="iProjection">Original projection</param>
        /// <param name="activeTimeline">Active branch</param>
        /// <param name="streamLocator">Stream locator</param>
        public HistoricalProjection(
            IEventStore<IAggregate> eventStore,
            ILog log,
            IProjection<TState> iProjection,
            ITimeline activeTimeline,
            IStreamLocator streamLocator)
            : base(eventStore, log, activeTimeline, streamLocator)
        {
            var projection = (ProjectionBase<TState>)iProjection;
            Predicate = projection.Predicate;
            foreach (var h in projection.Handlers)
                Register(h.Key, (e, s) => h.Value(e, s));
        }

        /// <inheritdoc />
        public long Timestamp { get; set; }
        
        /// <inheritdoc />
        protected override long Latest => Timestamp;
    }
}