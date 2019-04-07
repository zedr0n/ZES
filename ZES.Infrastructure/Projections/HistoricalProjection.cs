using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public class HistoricalProjection<T,TState> : Projection<TState> where T : Projection<TState>
                                                                     where TState : new()
    {
        private long _timestamp;

        public HistoricalProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline, T projection) 
            : base(eventStore, log, messageQueue, timeline)
        {
            foreach (var h in projection.Handlers)
                Register(h.Key, (e,s) => e.Timestamp <= _timestamp ? h.Value(e,s) : s);
        }

        public async Task Init(long timestamp)
        {
            Log.Trace("",this);
            _timestamp = timestamp;
            await Rebuild();
        }
    }
}