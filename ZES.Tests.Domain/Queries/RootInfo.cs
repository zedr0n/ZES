using NodaTime;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfo : ISingleState
    {
        public RootInfo()
        {
        }
        
        public RootInfo(string id, Time createdAt, Time updatedAt, int numberOfUpdates)
        {
            RootId = id;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            NumberOfUpdates = numberOfUpdates;
        }
        
        public string RootId { get; set; }
        public Time CreatedAt { get; set; }
        public Time UpdatedAt { get; set; }
        public int NumberOfUpdates { get; set; }
    }
}