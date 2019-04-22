using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public class HistoricalDecorator<TState> : Projection<TState>, IHistoricalProjection
        where TState : new()
    {
        private long _timestamp;

        public HistoricalDecorator(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, IProjection<TState> iProjection)
            : base(eventStore, log, messageQueue)
        {
            var projection = (Projection<TState>)iProjection;
            foreach (var h in projection.Handlers)
                Register(h.Key, (e, s) => e.Timestamp <= _timestamp ? h.Value(e, s) : s);
        }

        public async Task Init(long timestamp)
        {
            Log.Trace(string.Empty, this);
            _timestamp = timestamp;
            await Start();
        }

        protected override void OnInit() { }
    }
}