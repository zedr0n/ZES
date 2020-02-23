using System.Collections.Concurrent;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoProjection : SingleProjection<RootInfoProjection.Results>
    {
        public RootInfoProjection(
            IEventStore<IAggregate> eventStore,
            ILog log,
            IMessageQueue messageQueue,
            ITimeline timeline)
            : base(eventStore, log, timeline, messageQueue)
        {
            State = new Results();
            Register<RootCreated>(When);
            Register<RootUpdated>(When);
        }

        private static Results When(RootCreated e, Results state)
        {
            state.SetCreatedAt(e.RootId, e.Timestamp);
            return state;
        }
        
        private static Results When(RootUpdated e, Results state)
        {
            state.SetUpdatedAt(e.RootId, e.Timestamp);
            return state;
        }
        
        public class Results
        {
            private readonly ConcurrentDictionary<string, long> _createdAt = new ConcurrentDictionary<string, long>();
            private readonly ConcurrentDictionary<string, long> _updatedAt = new ConcurrentDictionary<string, long>();

            public int NumberOfUpdates { get; private set; }
            public long CreatedAt(string id) => _createdAt.TryGetValue(id, out var createdAt) ? createdAt : default(long);
            public long UpdatedAt(string id) => _updatedAt.TryGetValue(id, out var updatedAt) ? updatedAt : default(long);

            public void SetCreatedAt(string id, long timestamp)
            {
                _createdAt[id] = timestamp;
                _updatedAt[id] = timestamp;
            }
            
            public void SetUpdatedAt(string id, long timestamp)
            {
                _updatedAt[id] = timestamp;
                NumberOfUpdates++;
            }
        }
    }
}