using System.Collections.Concurrent;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Projections
{
    public class RootProjection : Projection<RootProjection.StateType>
    {
        public class StateType
        {
            private readonly ConcurrentDictionary<string, long> _createdAt = new ConcurrentDictionary<string, long>();

            public long Get(string id)
            {
                _createdAt.TryGetValue(id, out var createdAt);
                return createdAt;
            }

            public void Set(string id, long timestamp)
            {
                _createdAt[id] = timestamp;
            }
        }
        
        private static StateType When(RootCreated e, StateType state)
        {
            state.Set(e.RootId, e.Timestamp);
            return state;
        }

        public RootProjection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue, ITimeline timeline) : base(eventStore, logger, messageQueue, timeline)
        {
            State = new StateType();
            Register<RootCreated>(When);
        }
    }
}