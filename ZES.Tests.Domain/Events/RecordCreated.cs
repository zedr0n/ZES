using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RecordCreated : Event
    {
        public RecordCreated(string rootId)
        {
            RootId = rootId;
        }
        
        public string RootId { get; }
    }
}