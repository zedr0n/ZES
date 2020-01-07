using ZES.Infrastructure.Attributes;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RootUpdated : Event
    {
        public RootUpdated(string rootId)
        {
            RootId = rootId;
        }
        
        public string RootId { get; }
    }
}