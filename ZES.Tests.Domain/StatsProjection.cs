using ZES.Infrastructure;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Tests.Domain
{
    public class StatsProjection : Projection
    {
        private long _count;
        public StatsProjection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue) : base(eventStore, logger, messageQueue)
        {
            Register<RootCreated>(When);
        }

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
    }
}