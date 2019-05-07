using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class StatsProjection : Projection<ValueState<Stats>>
    {
        public StatsProjection(
            IEventStore<IAggregate> eventStore,
            ILog log,
            IMessageQueue messageQueue,
            ITimeline timeline,
            ProjectionDispatcher.Builder builder)
            : base(eventStore, log, messageQueue, timeline, builder)
        {
            Register<RootCreated>(When);
        }

        // public override string Key(IStream stream) => "Stats";

        private static ValueState<Stats> When(RootCreated e, ValueState<Stats> state)
        {
            lock (state)
            {
                state.Value.NumberOfRoots++; 
            }
            
            return state;
        }
    }
}