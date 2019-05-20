using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RootCreated : Event
    {
        public RootCreated(string rootId, Root.Type type)
        {
            RootId = rootId;
            Type = type;
        }

        public string RootId { get; }   
        public Root.Type Type { get; }
    }
}