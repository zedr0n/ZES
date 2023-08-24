using System;
using Newtonsoft.Json;
using ZES.Infrastructure.Serialization;

namespace ZES.Tests.Domain.Events
{
    public class RootRecordedDeserializer : EventDeserializerBase<RootRecorded>
    {
        public override void Switch(JsonTextReader reader, string currentProperty, RootRecorded e)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Float when currentProperty == nameof(RootRecorded.RecordValue):
                    e.RecordValue = (double)reader.Value;
                    break;
            }
        }
    }
}