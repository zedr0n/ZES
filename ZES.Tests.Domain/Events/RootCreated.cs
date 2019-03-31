using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RootCreated : Event
    {
        public RootCreated(string rootId)
        {
            RootId = rootId;
        }

        public string RootId { get; }   
    }
}