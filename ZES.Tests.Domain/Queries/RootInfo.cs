using NodaTime;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfo : ISingleState
    {
        public RootInfo()
        {
        }
        
        public RootInfo(string id, Instant createdAt, Instant updatedAt, int numberOfUpdates)
        {
            RootId = id;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            NumberOfUpdates = numberOfUpdates;
        }
        
        public string RootId { get; set; }
        public Instant CreatedAt { get; set; }
        public Instant UpdatedAt { get; set; }
        public int NumberOfUpdates { get; set; }
    }
}