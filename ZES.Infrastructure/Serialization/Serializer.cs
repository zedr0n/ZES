using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public class Serializer<T> : ISerializer<T>
        where T : class, IMessage
    {
        private readonly JsonSerializer _serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Serializer{T}"/> class.
        /// <para> Uses <see cref="TypeNameHandling.All"/> for JSON serialisation </para>
        /// </summary>
        public Serializer()
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public string EncodeMetadata(T message)
        {
            var version = (message as IEvent)?.Version ?? 0;

            var meta = new EventMetadata
            {
                AncestorId = message.AncestorId,
                Idempotent = message.Idempotent,
                MessageId = message.MessageId,
                Timestamp = message.Timestamp,
                Version = version
            };
            
            using (var writer = new StringWriter())
            {
                var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.None };

                _serializer.Serialize(jsonWriter, meta);

                // We don't close the stream as it's owned by the message.
                writer.Flush();
                return writer.ToString();
            }
        }

        /// <inheritdoc />
        public IEventMetadata DecodeMetadata(string json)
        {
            using (var reader = new StringReader(json))
            {
                var jsonReader = new JsonTextReader(reader);

                try
                {
                    return _serializer.Deserialize<EventMetadata>(jsonReader);
                }
                catch (Exception e)
                {
                    // Wrap in a standard .NET exception.
                    if (e is JsonSerializationException)
                        throw new SerializationException(e.Message, e);
                    
                    return null;
                }
            }
        }

        /// <inheritdoc />
        public T Deserialize(string payload)
        {
            using (var reader = new StringReader(payload))
            {
                var jsonReader = new JsonTextReader(reader);

                try
                {
                    return _serializer.Deserialize<T>(jsonReader);
                }
                catch (Exception e)
                {
                    // Wrap in a standard .NET exception.
                    if (e is JsonSerializationException)
                        throw new SerializationException(e.Message, e);
                    
                    return null;
                }
            }
        }
    }
}