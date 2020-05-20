using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecord : IState
    {
        public LastRecord() { }
        
        public string Id { get; }
        public long TimeStamp { get; set; } = long.MinValue;
        public double Value { get; set; } = -1;
    }
}