namespace ZES.Tests.Domain.Queries
{
    public class LastRecord
    {
        public LastRecord() { }
        
        public string Id { get; } 
        public long TimeStamp { get; set; }
        public double Value { get; set; }
    }
}