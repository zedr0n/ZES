using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public interface IHistoricalProjection
    {
        Task Init(long timestamp);
    }
    
    public class HistoricalDecorator<TState> : Projection<TState>, IHistoricalProjection where TState : new()
    {
        private long _timestamp;

        public HistoricalDecorator(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline, IProjection<TState> iProjection) : base(eventStore, log, messageQueue, timeline)
        {
            var projection = iProjection as Projection<TState>;
            foreach (var h in projection.Handlers)
                Register(h.Key, (e,s) => e.Timestamp <= _timestamp ? h.Value(e,s) : s);
        }

        public override void OnInit()
        {
        }

        public async Task Init(long timestamp)
        {
            Log.Trace("",this);
            _timestamp = timestamp;
            Start(false);
            await Rebuild();
        }
    }
}