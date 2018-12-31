using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Core.Domain
{
    public class Command : ICommand
    {
        public string AggregateId { get; set; }
        public long Timestamp { get; set; }
    }
}