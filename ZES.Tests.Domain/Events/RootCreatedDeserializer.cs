using System;
using Newtonsoft.Json;
using ZES.Infrastructure.Serialization;

namespace ZES.Tests.Domain.Events
{
    public class RootCreatedDeserializer : EventDeserializerBase<RootCreated, RootCreatedPayload>
    {
        public override void Switch(JsonTextReader reader, string currentProperty, RootCreated e)
        {
            switch (reader.TokenType)
            {
                case JsonToken.String when currentProperty == nameof(RootCreated.RootId):
                    e.Payload.RootId = (string)reader.Value;
                    break;
                case JsonToken.Integer when currentProperty == nameof(RootCreated.Type):
                    Enum.TryParse<Root.Type>(reader.Value.ToString(), out var type);
                    e.Payload.Type = type; 
                    break;
            }
        }
    }
}