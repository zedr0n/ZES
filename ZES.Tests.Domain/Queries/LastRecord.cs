namespace ZES.Tests.Domain.Queries
{
    public class LastRecord
    {
        public LastRecord() { }
        
        public string Id { get; }
        public long TimeStamp { get; set; } = long.MinValue;
        public double Value { get; set; } = -1;
    }
}