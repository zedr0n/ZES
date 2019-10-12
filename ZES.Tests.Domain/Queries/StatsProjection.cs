using System.Threading;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class StatsProjection : SingleProjection<Stats>
    {
        public StatsProjection(
            IEventStore<IAggregate> eventStore,
            ILog log,
            IMessageQueue messageQueue,
            ITimeline timeline)
            : base(eventStore, log, timeline, messageQueue)
        {
            Register<RootCreated>(When);
        }

        private static Stats When(RootCreated e, Stats state)
        {
            lock (state)
                state.NumberOfRoots++; 
            
            return state;
        }
    }
}