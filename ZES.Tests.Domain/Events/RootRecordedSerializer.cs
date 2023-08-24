using Newtonsoft.Json;
using ZES.Infrastructure.Serialization;

namespace ZES.Tests.Domain.Events
{
    public class RootRecordedSerializer : EventSerializerBase<RootRecorded>
    {
        public override void Write(JsonTextWriter writer, RootRecorded e)
        {
            writer.WritePropertyName(nameof(RootRecorded.RecordValue));
            writer.WriteValue(e.RecordValue);
        }
    }
}
