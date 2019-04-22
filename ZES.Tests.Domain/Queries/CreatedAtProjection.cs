using System.Collections.Concurrent;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class CreatedAtProjection : Projection<CreatedAtProjection.StateType>
    {
        public class StateType
        {
            private readonly ConcurrentDictionary<string, long> _createdAt = new ConcurrentDictionary<string, long>();

            public long Get(string id)
            {
                if(_createdAt.TryGetValue(id, out var createdAt))
                    return createdAt;
                return default(long);
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

        public CreatedAtProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue) : base(eventStore, log, messageQueue)
        {
            State = new StateType();
            Register<RootCreated>(When);
        }
    }
}