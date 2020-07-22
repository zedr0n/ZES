using Newtonsoft.Json;
using ZES.Infrastructure.Serialization;

namespace ZES.Tests.Domain.Events
{
    public class RootCreatedSerializer : EventSerializerBase<RootCreated>
    {
        public override void Write(JsonTextWriter writer, RootCreated e)
        {
            writer.WritePropertyName(nameof(RootCreated.RootId));
            writer.WriteValue(e.RootId);
            
            writer.WritePropertyName(nameof(RootCreated.Type));
            writer.WriteValue(e.Type);
        }
    }
}