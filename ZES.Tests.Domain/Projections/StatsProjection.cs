using ZES.Infrastructure;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Projections
{
    public class StatsProjection : Projection
    {
        private long _count;

        public long Get()
        {
            return _count;
        }

        protected override void Reset()
        {
            _count = 0;
        }

        private void When(RootCreated e)
        {
            _count++;
        }

        public StatsProjection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue, ITimeline timeline) : base(eventStore, logger, messageQueue, timeline)
        {
            Register<RootCreated>(When);
        }
    }
}