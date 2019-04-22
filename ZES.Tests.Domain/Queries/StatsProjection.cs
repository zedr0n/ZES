using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class StatsProjection : Projection<ValueState<Stats>>
    {
        private static ValueState<Stats> When(RootCreated e, ValueState<Stats> state)
        {
            lock (state)
            {
                state.Value.NumberOfRoots++; 
            }
            return state;
        }

        public StatsProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue) : base(eventStore, log, messageQueue)
        {
            Register<RootCreated>(When);
        }
    }
}