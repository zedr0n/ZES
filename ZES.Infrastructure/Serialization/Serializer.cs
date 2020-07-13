#define USE_EXPLICIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Jil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;
using Stream = ZES.Infrastructure.EventStore.Stream;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public class Serializer<T> : ISerializer<T>
        where T : class, IMessage
    {
        private readonly JsonSerializer _serializer;
        private readonly IEventDeserializerRegistry _deserializerRegistry;
#if USE_JSON        
        private readonly JsonSerializer _simpleSerializer;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="Serializer{T}"/> class.
        /// <para> Uses <see cref="TypeNameHandling.All"/> for JSON serialisation </para>
        /// </summary>
        /// <param name="deserializerRegistry">Deserializer collection</param>
        public Serializer(IEventDeserializerRegistry deserializerRegistry)
        {
            _deserializerRegistry = deserializerRegistry;
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                // Allows deserializing to the actual runtime type
                TypeNameHandling = TypeNameHandling.All,

                // In a version resilient way
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                Converters = new List<JsonConverter>(),
                DateParseHandling = DateParseHandling.None
            });
#if USE_JSON            
            _simpleSerializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                DateParseHandling = DateParseHandling.None,
                Converters = new List<JsonConverter>()
            });
#endif
#if USE_JIL            
            // warm up serializers
            JSON.Serialize(new EventMetadata
            {
                AncestorId = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Timestamp = 0,
                Version = 0
            });
            JSON.Serialize(new StreamMetadata("master:Root:0", 0));
#endif
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
        public T Deserialize(string payload)
        {
            using (var reader = new StringReader(payload))
            {
                var jsonReader = new JsonTextReader(reader);

                try
                {
#if USE_EXPLICIT
                    if (typeof(T) != typeof(IEvent)) 
                        return _serializer.Deserialize<T>(jsonReader);
                    
                    var e = DeserializeEvent(payload);
                    if (e != null)
                        return e as T;
#endif
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

        /// <inheritdoc />
        public string EncodeStreamMetadata(IStream stream)
        {
#if USE_EXPLICIT
            var meta = new JObject(
                new JProperty(nameof(IStream.Key), stream.Key),
                new JProperty(nameof(IStream.Version), stream.Version));

            if (stream.Parent != null)
            {
                var parent = new JObject(
                    new JProperty(nameof(IStream.Key), stream.Parent?.Key),
                    new JProperty(nameof(IStream.Version), stream.Parent?.Version)); 
                meta.Add(nameof(IStream.Parent), parent);
            }

            return meta.ToString();
#else            
            var meta = new StreamMetadata(stream.Key, stream.Version);
            
            if (stream.Parent != null)
            {
                var parent = new StreamMetadata(stream.Parent.Key, stream.Parent.Version);
                meta.Parent = parent;
            }
#if USE_UTF8
            return Utf8Json.JsonSerializer.ToJsonString(meta);
#elif USE_JIL
            return Jil.JSON.Serialize(meta);
#elif USE_JSON
            using (var writer = new StringWriter())
            {
                var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.None };

                _simpleSerializer.Serialize(jsonWriter, meta);

                // We don't close the stream as it's owned by the message.
                writer.Flush();
                return writer.ToString();
            }
#endif
#endif
        }

        /// <inheritdoc />
        public IStream DecodeStreamMetadata(string json)
        {
            if (json == null)
                return null;
#if USE_EXPLICIT
            var jarray = JObject.Parse(json);
            
            if (!jarray.TryGetValue(nameof(IStream.Version), out var version))
                return null;
            
            if (!jarray.TryGetValue(nameof(IStream.Key), out var key))
                return null;

            var stream = new Stream((string)key, (int)version);

            if (!jarray.TryGetValue(nameof(IStream.Parent), out var jParent))
                return stream;
            
            ((JObject)jParent).TryGetValue(nameof(IStream.Key), out var parentKey);
            ((JObject)jParent).TryGetValue(nameof(IStream.Version), out var parentVersion);
                
            stream.Parent = new Stream((string)parentKey, (int)parentVersion);

            return stream;
#else            
            StreamMetadata meta;

#if USE_UTF8
            meta = Utf8Json.JsonSerializer.Deserialize<StreamMetadata>(json);
#elif USE_JIL
            meta = Jil.JSON.Deserialize<StreamMetadata>(json);
#elif USE_JSON            
            using (var reader = new StringReader(json))
            {
                var jsonReader = new JsonTextReader(reader);

                try
                {
                    meta = _simpleSerializer.Deserialize<StreamMetadata>(jsonReader);
                }
                catch (Exception e)
                {
                    // Wrap in a standard .NET exception.
                    if (e is JsonSerializationException)
                        throw new SerializationException(e.Message, e);
                    
                    return null;
                }
            }
#endif            
            var stream = new Streams.Stream(meta.Key, meta.Version);
            if (meta.Parent != null)
                stream.Parent = new Streams.Stream(meta.Parent.Key, meta.Parent.Version);

            return stream;
#endif
        }

        /// <inheritdoc />
        public string EncodeMetadata(T message)
        {
            var e = message as IEvent;
            var version = e?.Version ?? 0;
            var hash = e?.Hash ?? string.Empty;
#if USE_EXPLICIT
            var meta = new JObject(
                new JProperty(nameof(IEventMetadata.MessageId), message.MessageId),
                new JProperty(nameof(IEventMetadata.AncestorId), message.AncestorId),
                new JProperty(nameof(IEventMetadata.Timestamp), message.Timestamp),
                new JProperty(nameof(IEventMetadata.Version), version),
                new JProperty(nameof(IEventMetadata.MessageType), message.GetType().Name),
                new JProperty(nameof(IEventMetadata.Hash), hash));

            return meta.ToString();
#else
            var meta = new EventMetadata
            {
                AncestorId = message.AncestorId,
                MessageId = message.MessageId,
                Timestamp = message.Timestamp,
                Version = version
            };
           
#if USE_UTF8
            return Utf8Json.JsonSerializer.ToJsonString(meta);
#elif USE_JIL
            return Jil.JSON.Serialize(meta,  Options.IncludeInherited);
#elif USE_JSON
            using (var writer = new StringWriter())
            {
                var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.None };

                _simpleSerializer.Serialize(jsonWriter, meta);

                // We don't close the stream as it's owned by the message.
                writer.Flush();
                return writer.ToString();
            }
#endif
#endif
        }

        /// <inheritdoc />
        /// <example> {
        /// "MessageId": "00782936-41af-47bb-8c32-b1fa4e240372",
        /// "AncestorId": "1d24d8a8-669c-487f-b1ca-9e36f7fd86db",
        /// "Timestamp": 1583368437195,
        /// "Version": 0,
        /// "MessageType": "RecordCreated",
        /// "Hash": "" }
        /// </example>
        public IEventMetadata DecodeMetadata(string json)
        {
#if USE_EXPLICIT
            var reader = new JsonTextReader(new StringReader(json));
            
            var metadata = new EventMetadata();
            var currentProperty = string.Empty;
            while (reader.Read())
            {
                if (reader.Value == null) 
                    continue;
                
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        currentProperty = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.AncestorId):
                        metadata.AncestorId = Guid.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.MessageId):
                        metadata.MessageId = Guid.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.Integer when currentProperty == nameof(IEventMetadata.Timestamp):
                        metadata.Timestamp = (long)reader.Value;
                        break;
                    case JsonToken.Integer when currentProperty == nameof(IEventMetadata.Version):
                        metadata.Version = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.MessageType):
                        metadata.MessageType = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.Hash):
                        metadata.Hash = (string)reader.Value;
                        break;
                }
            }

            return metadata;
#elif USE_JOBJECT
            var jarray = JObject.Parse(json);
            
            var isValid = true;
            isValid &= jarray.TryGetValue(nameof(IEventMetadata.AncestorId), out var jAncestorId);
            isValid &= jarray.TryGetValue(nameof(IEventMetadata.Timestamp), out var jTimestamp);
            isValid &= jarray.TryGetValue(nameof(IEventMetadata.Version), out var jVersion);
            isValid &= jarray.TryGetValue(nameof(IEventMetadata.MessageId), out var jMessageId);
            isValid &= jarray.TryGetValue(nameof(IEventMetadata.MessageType), out var jEventType);
            isValid &= jarray.TryGetValue(nameof(IEventMetadata.Hash), out var jHash);

            if (!isValid)
                return null;
            return new EventMetadata
            {
                MessageId = (Guid)jMessageId,
                AncestorId = (Guid)jAncestorId,
                Timestamp = (long)jTimestamp,
                Version = (int)jVersion,
                MessageType = (string)jEventType,
                Hash = (string)jHash
            };
#else
#if USE_UTF8
            return Utf8Json.JsonSerializer.Deserialize<EventMetadata>(json);
#elif USE_JIL
            return Jil.JSON.Deserialize<EventMetadata>(json, Options.IncludeInherited);
#elif USE_JSON
            using (var reader = new StringReader(json))
            {
                var jsonReader = new JsonTextReader(reader);

                try
                {
                    return _simpleSerializer.Deserialize<EventMetadata>(jsonReader);
                }
                catch (Exception e)
                {
                    // Wrap in a standard .NET exception.
                    if (e is JsonSerializationException)
                        throw new SerializationException(e.Message, e);
                    
                    return null;
                }
            }
#endif
#endif
        }

#if USE_EXPLICIT
#else        
        /// <summary>
        /// Stream metadata class
        /// </summary>
        public class StreamMetadata
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="StreamMetadata"/> class.
            /// </summary>
            public StreamMetadata() { }
            
            /// <summary>
            /// Initializes a new instance of the <see cref="StreamMetadata"/> class.
            /// </summary>
            /// <param name="key">Stream key</param>
            /// <param name="version">Stream version</param>
            public StreamMetadata(string key, int version)
            {
                Key = key;
                Version = version;
            }
                
            /// <summary>
            /// Gets or sets stream key 
            /// </summary>
            public string Key { get; set; }
            
            /// <summary>
            /// Gets or sets stream version 
            /// </summary>
            public int Version { get; set; }
            
            /// <summary>
            /// Gets or sets metadata of parent stream if any
            /// </summary>
            public StreamMetadata Parent { get; set; }
        }
#endif
        
        #if USE_EXPLICIT
        private Event DeserializeEvent(string payload)
        {
            if (payload == null)
                return null;

            var reader = new JsonTextReader(new StringReader(payload));

            var deserializer = _deserializerRegistry.GetDeserializer(payload);
            if (deserializer == null)
                return null;

            var e = deserializer.Create();
            
            var currentProperty = string.Empty;
            while (reader.Read())
            {
                if (reader.Value == null)
                    continue;
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        currentProperty = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(Message.MessageId):
                        e.MessageId = Guid.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(Message.AncestorId):
                        e.AncestorId = Guid.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.Integer when currentProperty == nameof(Message.Timestamp):
                        e.Timestamp = (long)reader.Value;
                        break;
                    case JsonToken.Integer when currentProperty == nameof(EventMetadata.Version):
                        e.Version = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.MessageType):
                        e.MessageType = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.Hash):
                        e.Hash = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(Event.CommandId):
                        e.CommandId = Guid.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(Event.Stream):
                        e.Stream = (string)reader.Value;
                        break;
                }
                
                deserializer.Switch(reader, currentProperty, e);
            }

            return e;
        }
        #endif
        
    }
}
