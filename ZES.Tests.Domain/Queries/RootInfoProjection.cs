using System.Collections.Concurrent;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoProjection : Projection<RootInfoProjection.StateType>
    {
        public RootInfoProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue)
            : base(eventStore, log, messageQueue)
        {
            State = new StateType();
            Register<RootCreated>(When);
            Register<RootUpdated>(When);
        }

        private static StateType When(RootCreated e, StateType state)
        {
            state.SetCreatedAt(e.RootId, e.Timestamp);
            return state;
        }
        
        private static StateType When(RootUpdated e, StateType state)
        {
            state.SetUpdatedAt(e.RootId, e.Timestamp);
            return state;
        }
        
        public class StateType
        {
            private readonly ConcurrentDictionary<string, long> _createdAt = new ConcurrentDictionary<string, long>();
            private readonly ConcurrentDictionary<string, long> _updatedAt = new ConcurrentDictionary<string, long>();

            public long CreatedAt(string id) => _createdAt.TryGetValue(id, out var createdAt) ? createdAt : default(long);
            public long UpdatedAt(string id) => _updatedAt.TryGetValue(id, out var updatedAt) ? updatedAt : default(long);

            public void SetCreatedAt(string id, long timestamp)
            {
                _createdAt[id] = timestamp;
            }
            
            public void SetUpdatedAt(string id, long timestamp)
            {
                _updatedAt[id] = timestamp;
            }
        }
    }
}