using System.Collections.Concurrent;
using ZES.Infrastructure.Projections;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoProjectionResults : IState
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

    public class RootCreatedProjectionHandler : ProjectionHandlerBase<RootInfoProjectionResults, RootCreated>
    {
        public override RootInfoProjectionResults Handle(RootCreated e, RootInfoProjectionResults state)
        {
            state.SetCreatedAt(e.RootId, e.Timestamp);
            return state;
        }
    }
    
    public class RootUpdatedProjectionHandler : ProjectionHandlerBase<RootInfoProjectionResults, RootUpdated>
    {
        public override RootInfoProjectionResults Handle(RootUpdated e, RootInfoProjectionResults state)
        {
            state.SetUpdatedAt(e.RootId, e.Timestamp);
            return state;
        }
    }
}