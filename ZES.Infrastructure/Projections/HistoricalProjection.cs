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
    public class HistoricalProjection<T,TState> : Projection<TState> where T : IProjection<TState>
                                                                     where TState : new()
    {
        private long _timestamp;

        public HistoricalProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline, T projection) 
            : base(eventStore, log, messageQueue, timeline)
        {
            var p = projection as Projection<TState>;
            foreach (var h in p.Handlers)
                Register(h.Key, (e,s) => e.Timestamp <= _timestamp ? h.Value(e,s) : s);
        }

        public async Task Init(long timestamp)
        {
            Log.Trace("",this);
            _timestamp = timestamp;
            await Rebuild();
        }
    }

    public interface IHistoricalProjection
    {
        Task Init(long timestamp);
    }
    
    public class HistoricalDecorator<TState> : Projection<TState>, IHistoricalProjection where TState : new()
    {
        private long _timestamp;
        private readonly Projection<TState> _projection;
        public HistoricalDecorator(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline, IProjection<TState> projection) : base(eventStore, log, messageQueue, timeline)
        {
            _projection = projection as Projection<TState>;
            foreach (var h in _projection.Handlers)
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