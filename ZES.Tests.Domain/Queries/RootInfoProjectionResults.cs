using System.Collections.Concurrent;
using ZES.Interfaces.Domain;

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
}