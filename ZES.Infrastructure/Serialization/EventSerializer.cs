using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Serialization
{
    public class EventSerializer : IEventSerializer
    {
        private readonly JsonSerializer _serializer;

        public EventSerializer()
        {
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                // Allows deserializing to the actual runtime type
                TypeNameHandling = TypeNameHandling.All,
                // In a version resilient way
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                Converters = new List<JsonConverter>(),
                DateParseHandling = DateParseHandling.None
            });
        }
        
        public string Serialize(IEvent e)
        {
            using (var writer = new StringWriter())
            {
                var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented };

                _serializer.Serialize(jsonWriter, e);

                // We don't close the stream as it's owned by the message.
                writer.Flush();
                return writer.ToString();
            }
        }

        public IEvent Deserialize(string payload)
        {
            using (var reader = new StringReader(payload))
            {
                var jsonReader = new JsonTextReader(reader);

                try
                {
                    return (Event) _serializer.Deserialize(jsonReader);
                }
                catch (JsonSerializationException e)
                {
                    // Wrap in a standard .NET exception.
                    throw new SerializationException(e.Message, e);
                }
            }
        }
    }
}