using NodaTime;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecord : IState
    {
        public LastRecord() { }
        
        public string Id { get; }
        public Instant TimeStamp { get; set; } = Instant.MinValue;
        public double Value { get; set; } = -1;
    }
}