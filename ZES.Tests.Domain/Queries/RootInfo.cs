namespace ZES.Tests.Domain.Queries
{
    public class RootInfo
    {
        public RootInfo(string id, long createdAt, long updatedAt)
        {
            RootId = id;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }
        
        public string RootId { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
    }
}