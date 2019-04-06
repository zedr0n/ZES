using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Serialization
{
    public class Serializer<T> : ISerializer<T> where T : class
    {
        private readonly JsonSerializer _serializer;
        private readonly ILog _log;

        public Serializer(ILog log)
        {
            _log = log;
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
        
        public string Serialize(T e)
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

        public string Metadata(long timestamp)
        {
            //var array = new JObject(new JProperty("metadata",new JObject(new JProperty("timestamp",timestamp))));
            var array = new JObject(new JProperty("timestamp",timestamp));
            var s = array.ToString();
            _log.Debug(s);
            return s;
        }

        public T Deserialize(string payload)
        {
            using (var reader = new StringReader(payload))
            {
                var jsonReader = new JsonTextReader(reader);

                try
                {
                    return (T) _serializer.Deserialize(jsonReader);
                }
                catch (JsonSerializationException e)
                {
                    // Wrap in a standard .NET exception.
                    throw new SerializationException(e.Message, e);
                }
            }
        }
    }
    
    public class EventSerializer : Serializer<IEvent>, IEventSerializer {
        public EventSerializer(ILog log) : base(log)
        {
        }
    }
    public class CommandSerializer : Serializer<ICommand>, ICommandSerializer {
        public CommandSerializer(ILog log) : base(log)
        {
        }
    }
}
