using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Projections
{
    public class StatsProjection : Projection<ValueState<long>>
    {
        private static ValueState<long> When(RootCreated e, ValueState<long> state)
        {
            lock (state)
            {
                state.Value++; 
            }
            return state;
        }

        public StatsProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline) : base(eventStore, log, messageQueue, timeline)
        {
            Register<RootCreated>(When);
        }
    }
}