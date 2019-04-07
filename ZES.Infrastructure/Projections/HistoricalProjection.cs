using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public class HistoricalProjection<T,TState> : Projection<TState> where T : Projection<TState>
                                                                     where TState : new()
    {
        private long _timestamp;

        public HistoricalProjection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue, ITimeline timeline, T projection) 
            : base(eventStore, logger, messageQueue, timeline)
        {
            State = new TState();
            foreach (var h in projection.Handlers)
                Register(h.Key, (e,s) => Timeline.Now <= _timestamp ? h.Value(e,s) : s);
        }

        public void Init(long timestamp)
        {
            _timestamp = timestamp;
        }
    }
}