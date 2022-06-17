using NodaTime;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecord : IState
    {
        public LastRecord() { }
        
        public string Id { get; }
        public Time TimeStamp { get; set; } = Time.MinValue;
        public double Value { get; set; } = -1;
    }
}