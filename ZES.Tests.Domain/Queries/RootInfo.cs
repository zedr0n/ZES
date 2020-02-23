namespace ZES.Tests.Domain.Queries
{
    public class RootInfo
    {
        public RootInfo(string id, long createdAt, long updatedAt, int numberOfUpdates)
        {
            RootId = id;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            NumberOfUpdates = numberOfUpdates;
        }
        
        public string RootId { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public int NumberOfUpdates { get; set; }
    }
}