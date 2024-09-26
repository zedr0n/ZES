using Newtonsoft.Json;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RootCreated : Event<RootCreatedPayload>
    {
        public RootCreated() { }
        public RootCreated(string rootId, Root.Type type)
        {
            Payload = new RootCreatedPayload { RootId = rootId, Type = type };
        }

        [JsonIgnore]
        public string RootId => Payload.RootId; 
        
        [JsonIgnore]
        public Root.Type Type => Payload.Type;
    }
    
    public class RootCreatedPayload
    {
        public string RootId { get; set; }   
        public Root.Type Type { get; set; }
    }
}