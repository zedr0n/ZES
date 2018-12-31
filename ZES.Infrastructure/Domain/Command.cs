using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    public class Command : ICommand
    {
        public string AggregateId { get; set; }
        public long Timestamp { get; set; }
    }
}