using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RootCreated : Event
    {
        public RootCreated() { }
        public RootCreated(string rootId, Root.Type type)
        {
            RootId = rootId;
            Type = type;
        }

        public string RootId { get; set; }   
        public Root.Type Type { get; set; }
    }
}