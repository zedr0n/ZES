using System.Collections.Concurrent;
using ZES.Infrastructure;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Projections
{
    public class RootProjection : Projection
    {
        private readonly ConcurrentDictionary<string, long> _createdAt = new ConcurrentDictionary<string, long>();
        

        public long Get(string id)
        {
            _createdAt.TryGetValue(id, out var createdAt);
            return createdAt;
        }

        private void When(RootCreated e)
        {
            _createdAt[e.RootId] = e.Timestamp;
        }

        public RootProjection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue, ITimeline timeline) : base(eventStore, logger, messageQueue, timeline)
        {
            Register<RootCreated>(When);
        }
    }
}