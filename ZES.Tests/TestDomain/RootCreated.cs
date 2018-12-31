using ZES.Infrastructure.Domain;

namespace ZES.Tests.TestDomain
{
    public class RootCreated : Event
    {
        public RootCreated(string rootId)
        {
            RootId = rootId;
            EventType = "RootCreated";
        }

        public string RootId { get; }   
    }
}