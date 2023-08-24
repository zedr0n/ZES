using System;
using Newtonsoft.Json;
using ZES.Infrastructure.Serialization;

namespace ZES.Tests.Domain.Events
{
    public class RecordCreatedDeserializer : EventDeserializerBase<RecordCreated>
    {
        public override void Switch(JsonTextReader reader, string currentProperty, RecordCreated e)
        {
            switch (reader.TokenType)
            {
                case JsonToken.String when currentProperty == nameof(RecordCreated.RootId):
                    e.RootId = (string)reader.Value;
                    break;
            }
        }
    }
}