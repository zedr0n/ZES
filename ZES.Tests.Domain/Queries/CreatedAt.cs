namespace ZES.Tests.Domain.Queries
{
    public class CreatedAt
    {
        public CreatedAt(string id, long timestamp)
        {
            RootId = id;
            Timestamp = timestamp;
        }
        
        public string RootId { get; set; }
        public long Timestamp { get; set; }
    }
}