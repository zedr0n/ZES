using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RecordCreated : Event
    {
        public string RootId { get; set; }
    }
}