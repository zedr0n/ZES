using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Projections
{
    public class StatsProjection : Projection<StatsProjection.StateType>
    {
        public class StateType
        {
            public long Value { get; set; }
        }
        
        private StateType When(RootCreated e, StateType state)
        {
            lock (state)
            {
                state.Value++; 
            }
            return state;
        }

        public StatsProjection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue, ITimeline timeline) : base(eventStore, logger, messageQueue, timeline)
        {
            Register<RootCreated>(When);
        }
    }
}