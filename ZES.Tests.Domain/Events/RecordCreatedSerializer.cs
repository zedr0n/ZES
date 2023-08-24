using Newtonsoft.Json;
using ZES.Infrastructure.Serialization;

namespace ZES.Tests.Domain.Events
{
    public class RecordCreatedSerializer : EventSerializerBase<RecordCreated>
    {
        public override void Write(JsonTextWriter writer, RecordCreated e)
        {
            writer.WritePropertyName(nameof(RootCreated.RootId));
            writer.WriteValue(e.RootId);
        }
    }
}
