#define USE_EXPLICIT

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using ZES.Infrastructure.Clocks;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;
using Stream = ZES.Infrastructure.EventStore.Stream;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public class JsonArrayPool : IArrayPool<char>
    {
        public static readonly JsonArrayPool Instance = new JsonArrayPool();

        /// <inheritdoc />
        public char[] Rent(int minimumLength)
        {
            // get char array from System.Buffers shared pool
            return ArrayPool<char>.Shared.Rent(minimumLength);
        }

        /// <inheritdoc />
        public void Return(char[] array)
        {
            // return char array to System.Buffers shared pool
            ArrayPool<char>.Shared.Return(array);
        }
    }    
    
    /// <inheritdoc />
    public class AutomaticJsonNameTable : DefaultJsonNameTable
    {
        private int _nAutoAdded = 0;
        private readonly int _maxToAutoAdd;

        /// <inheritdoc />
        public AutomaticJsonNameTable(int maxToAdd)
        {
            _maxToAutoAdd = maxToAdd;
        }

        /// <inheritdoc />
        public override string Get(char[] key, int start, int length)
        {
            var s = base.Get(key, start, length);

            if (s != null || _nAutoAdded >= _maxToAutoAdd) 
                return s;
            
            s = new string(key, start, length);
            Add(s);
            _nAutoAdded++;

            return s;
        }
    }    
    
    /// <inheritdoc />
    public class Serializer<T> : ISerializer<T>
        where T : class
    {
        private readonly ILog _log;
        private readonly JsonSerializer _serializer;
        private readonly IEventSerializationRegistry _serializationRegistry;

        private const int MaxPropertyNamesToCache = 200;
        private readonly AutomaticJsonNameTable _jsonNameTable;
        private readonly IArrayPool<char> _jsonArrayPool;
#if USE_JSON        
        private readonly JsonSerializer _simpleSerializer;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="Serializer{T}"/> class.
        /// <para> Uses <see cref="TypeNameHandling.All"/> for JSON serialisation </para>
        /// </summary>
        /// <param name="serializationRegistry">Deserializer collection</param>
        /// <param name="log">Log service</param>
        public Serializer(IEventSerializationRegistry serializationRegistry, ILog log)
        {
            _serializationRegistry = serializationRegistry;
            _log = log;

            _jsonNameTable = null;
            _jsonArrayPool = null;
            if (Configuration.UseJsonNameTable)
                _jsonNameTable = new AutomaticJsonNameTable(MaxPropertyNamesToCache);
            if (Configuration.UseJsonArrayPool)
                _jsonArrayPool = JsonArrayPool.Instance;
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                // Allows deserializing to the actual runtime type
                TypeNameHandling = TypeNameHandling.Objects,

                // In a version resilient way
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                Converters = new List<JsonConverter>(),
                DateParseHandling = DateParseHandling.None,
            }).ConfigureForTime(); 
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
            string payload = null;
            using (var writer = new StringWriter())
            {
                var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
#if USE_EXPLICIT
                payload = SerializeEvent(e as IEvent);

                if (payload != null) 
                    return payload;
                
                _serializer.Serialize(jsonWriter, e);
                writer.Flush();
                payload = writer.ToString();
#else
                _serializer.Serialize(jsonWriter, e);

                // We don't close the stream as it's owned by the message.
                writer.Flush();
                payload = writer.ToString();
#endif
            }

            return payload;
        }

        /// <inheritdoc />
        public void SerializeEventAndMetadata(T e, out string eventJson, out string metadataJson, IEnumerable<string> ignoredProperties = null)
        {
            _log.StopWatch.Start(nameof(SerializeEventAndMetadata));
            eventJson = null;
            metadataJson = null;

#if USE_EXPLICIT
            SerializeEventAndMetadata(e as IEvent, out eventJson, out metadataJson, ignoredProperties);
            
            if (eventJson == null || metadataJson == null)
            {
                eventJson = Serialize(e);
                metadataJson = EncodeMetadata(e);
            }
#else
            eventJson = Serialize(e);
            metadataJson = EncodeMetadata(e);
#endif
            _log.StopWatch.Stop(nameof(SerializeEventAndMetadata));
        }

        /// <inheritdoc />
        public T Deserialize(string payload, bool full = true)
        {
            T result = null;
            _log.StopWatch.Start(nameof(Deserialize));
            using (var reader = new StringReader(payload))
            {
                var jsonReader = new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable };
                if(_jsonArrayPool != null)
                    jsonReader.ArrayPool = _jsonArrayPool;

                try
                {
#if USE_EXPLICIT
                    if (typeof(T) != typeof(IEvent))
                        result = _serializer.Deserialize<T>(jsonReader);
                    else
                        result = DeserializeEvent(payload, full) as T;

#endif
                    if (result == null)
                        result = _serializer.Deserialize<T>(jsonReader);
                }
                catch (Exception e)
                {
                    // Wrap in a standard .NET exception.
                    if (e is JsonSerializationException)
                        throw new SerializationException(e.Message, e);
                }

                _log.StopWatch.Stop(nameof(Deserialize));
                return result;
            }
        }

        /// <inheritdoc />
        public string EncodeStreamMetadata(IStream stream)
        {
#if USE_EXPLICIT
            using (var writer = new StringWriter())
            {
                var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented};
                if(_jsonArrayPool != null)
                    jsonWriter.ArrayPool = _jsonArrayPool;

                jsonWriter.WriteStartObject();
                
                jsonWriter.WritePropertyName(nameof(IStream.Key));
                jsonWriter.WriteValue(stream.Key);
                
                jsonWriter.WritePropertyName(nameof(IStream.Version));
                jsonWriter.WriteValue(stream.Version);

                if (stream.SnapshotVersion > 0)
                {
                    jsonWriter.WritePropertyName(nameof(IStream.SnapshotVersion));
                    jsonWriter.WriteValue(stream.SnapshotVersion);

                    jsonWriter.WritePropertyName(nameof(IStream.SnapshotTimestamp));
                    jsonWriter.WriteValue(stream.SnapshotTimestamp.ToExtendedIso());
                }

                if (stream.Parent != null)
                {
                    jsonWriter.WritePropertyName($"Parent{nameof(IStream.Key)}");
                    jsonWriter.WriteValue(stream.Parent.Key);
                    
                    jsonWriter.WritePropertyName($"Parent{nameof(IStream.Version)}");
                    jsonWriter.WriteValue(stream.Parent.Version);

                    if (stream.Parent.SnapshotVersion > 0)
                    {
                        jsonWriter.WritePropertyName($"Parent{nameof(IStream.SnapshotVersion)}");
                        jsonWriter.WriteValue(stream.Parent.SnapshotVersion);
                    
                        jsonWriter.WritePropertyName($"Parent{nameof(IStream.SnapshotTimestamp)}");
                        jsonWriter.WriteValue(stream.Parent.SnapshotTimestamp.ToExtendedIso());
                    }
                }
                
                jsonWriter.WriteEndObject();
                return writer.ToString();
            }
            
            /*var meta = new JObject(
                new JProperty(nameof(IStream.Key), stream.Key),
                new JProperty(nameof(IStream.Version), stream.Version));
            if (stream.Parent != null)
            {
                var parent = new JObject(
                    new JProperty(nameof(IStream.Key), stream.Parent?.Key),
                    new JProperty(nameof(IStream.Version), stream.Parent?.Version)); 
                meta.Add(nameof(IStream.Parent), parent);
            }

            return meta.ToString();*/
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
#else
            using (var writer = new StringWriter())
            {
                var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.None };

                _serializer.Serialize(jsonWriter, meta);

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
            if (json == null || json == "{}")
                return null;
#if USE_EXPLICIT

            var reader = new JsonTextReader(new StringReader(json))
                { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable };
            if(_jsonArrayPool != null)
                reader.ArrayPool = _jsonArrayPool;
            
            var currentProperty = string.Empty;
            var key = string.Empty;
            var version = ExpectedVersion.EmptyStream;
            var snapshotVersion = 0;
            var snapshotTimestamp = Time.MinValue;
            var parentKey = string.Empty;
            var parentVersion = ExpectedVersion.NoStream;
            var parentSnapshotVersion = 0;
            var parentSnapshotTimestamp = Time.MinValue;
            while (reader.Read())
            {
                if (reader.Value == null)
                    continue;

                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        currentProperty = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(IStream.Key):
                        key = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == $"Parent{nameof(IStream.Key)}":
                        parentKey = (string)reader.Value;
                        break;
                    case JsonToken.Integer when currentProperty == nameof(IStream.Version):
                        version = (int)(long)reader.Value;
                        break;
                    case JsonToken.Integer when currentProperty == $"Parent{nameof(IStream.Version)}":
                        parentVersion = (int)(long)reader.Value;
                        break;
                    case JsonToken.Integer when currentProperty == nameof(IStream.SnapshotVersion):
                        snapshotVersion = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IStream.SnapshotTimestamp):
                        snapshotTimestamp = Time.FromExtendedIso((string)reader.Value);
                        break;
                    case JsonToken.Integer when currentProperty == $"Parent{nameof(IStream.SnapshotVersion)}":
                        parentSnapshotVersion = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == $"Parent{nameof(IStream.SnapshotTimestamp)}":
                        parentSnapshotTimestamp = Time.FromExtendedIso((string)reader.Value); 
                        break;
                }
            }
            
            var stream = new Stream(key, version)
            {
                SnapshotVersion = snapshotVersion,
                SnapshotTimestamp = snapshotTimestamp,
            };
            if (parentKey != string.Empty)
            {
                stream.Parent = new Stream(parentKey, parentVersion)
                {
                    SnapshotVersion = parentSnapshotVersion,
                    SnapshotTimestamp = parentSnapshotTimestamp,
                };
            }

            return stream;
#else
            StreamMetadata meta;

#if USE_UTF8
            meta = Utf8Json.JsonSerializer.Deserialize<StreamMetadata>(json);
#elif USE_JIL
            meta = Jil.JSON.Deserialize<StreamMetadata>(json);
#else            
            using (var reader = new StringReader(json))
            {
                var jsonReader = new JsonTextReader(reader);

                try
                {
                    meta = _serializer.Deserialize<StreamMetadata>(jsonReader);
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
            var stream = new Stream(meta.Key, meta.Version);
            if (meta.Parent != null)
                stream.Parent = new Stream(meta.Parent.Key, meta.Parent.Version);

            return stream;
#endif
        }

        /// <inheritdoc />
        public string EncodeMetadata(T message)
        {
            var e = message as IEvent;
            if (e == null)
                return null;
            var version = e?.Version ?? 0;
            var hash = e?.ContentHash ?? string.Empty;
#if USE_EXPLICIT
            
            var sw = new StringWriter();
            var writer = new JsonTextWriter(sw);

            // {
            writer.WriteStartObject();
            
            writer.WritePropertyName(nameof(IEventMetadata.MessageId));
            writer.WriteValue(e.MessageId.ToString());

            if (e.AncestorId != default)
            {
                writer.WritePropertyName(nameof(IEventMetadata.AncestorId));
                writer.WriteValue(e.AncestorId.ToString());
            }

            if (e.CorrelationId != null)
            {
                writer.WritePropertyName(nameof(IEventMetadata.CorrelationId));
                writer.WriteValue(e.CorrelationId);
            }

            writer.WritePropertyName(nameof(IEventMetadata.Timestamp));
            writer.WriteValue(e.Timestamp.ToExtendedIso());
            
            writer.WritePropertyName(nameof(IEventMetadata.LocalId));
            writer.WriteValue(e.LocalId.ToString());
            
            writer.WritePropertyName(nameof(IEventMetadata.OriginId));
            writer.WriteValue(e.OriginId.ToString());
            
            writer.WritePropertyName(nameof(IEventMetadata.StreamHash));
            writer.WriteValue(e.StreamHash);

            writer.WritePropertyName(nameof(IEventMetadata.Version));
            writer.WriteValue(version);
            
            writer.WritePropertyName(nameof(IEventMetadata.MessageType));
            writer.WriteValue(e.GetType().Name);
            
            writer.WritePropertyName(nameof(IEventMetadata.ContentHash));
            writer.WriteValue(hash);
            
            writer.WritePropertyName(nameof(IEventMetadata.Timeline));
            writer.WriteValue(e.Timeline);
            
            writer.WriteEndObject();
            return sw.ToString();
#else
            var meta = new JObject(
                new JProperty(nameof(IEventMetadata.MessageId), e.MessageId),
                new JProperty(nameof(IEventMetadata.AncestorId), e.AncestorId),
                new JProperty(nameof(IEventMetadata.Timestamp), e.Timestamp.ToExtendedIso()),
                new JProperty(nameof(IEventMetadata.LocalId), e.LocalId.ToString()),
                new JProperty(nameof(IEventMetadata.OriginId), e.OriginId.ToString()),
                new JProperty(nameof(IEventMetadata.Version), version),
                new JProperty(nameof(IEventMetadata.MessageType), message.GetType().Name),
                new JProperty(nameof(IEventMetadata.Hash), hash));
#if USE_UTF8
            return Utf8Json.JsonSerializer.ToJsonString(meta);
#elif USE_JIL
            return Jil.JSON.Serialize(meta,  Options.IncludeInherited);
#else
            using (var writer = new StringWriter())
            {
                var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.None };

                _serializer.Serialize(jsonWriter, meta);

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
        /// "CorrelationId" : "",
        /// "Timestamp": 1583368437195,
        /// "Version": 0,
        /// "MessageType": "RecordCreated",
        /// "Hash": "" }
        /// </example>
        public IEventMetadata DecodeMetadata(string json)
        {
            _log.StopWatch.Start(nameof(DecodeMetadata));
#if USE_EXPLICIT
            var reader = new JsonTextReader(new StringReader(json))
                { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable };
            if(_jsonArrayPool != null)
                reader.ArrayPool = _jsonArrayPool;
            
            var metadata = new EventMetadata();
            var currentProperty = string.Empty;
            metadata.CorrelationId = null;
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
                        metadata.AncestorId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.CorrelationId):
                        metadata.CorrelationId = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.MessageId):
                        metadata.MessageId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.Timestamp):
                        metadata.Timestamp = Time.FromExtendedIso((string)reader.Value);
                        break;
                    case JsonToken.Integer when currentProperty == nameof(IEventMetadata.Version):
                        metadata.Version = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.MessageType):
                        metadata.MessageType = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.ContentHash):
                        metadata.ContentHash = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.StreamHash):
                        metadata.StreamHash = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.Timeline):
                        metadata.Timeline = (string)reader.Value;
                        break;
                }
            }

            _log.StopWatch.Stop(nameof(DecodeMetadata));
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
#else
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

        private bool WritePropertyName(JsonWriter writer, string name, IEnumerable<string> ignoredProperties)
        {
            if (ignoredProperties != null)
            {
                if (ignoredProperties.Contains(name))
                    return false;
            }

            writer.WritePropertyName(name);
            return true;
        }

        private void WriteEventMetadata(JsonWriter writer, IEventMetadata e, IEnumerable<string> ignoredProperties = null)
        {
            var properties = ignoredProperties as string[] ?? ignoredProperties?.ToArray();
            if (WritePropertyName(writer, nameof(IEventMetadata.MessageId), properties))
                writer.WriteValue(e.MessageId.ToString());
            
            if (WritePropertyName(writer, nameof(IEventMetadata.AncestorId), properties))
                writer.WriteValue(e.AncestorId.ToString());

            if (WritePropertyName(writer, nameof(IEventMetadata.CorrelationId), properties))
                writer.WriteValue(e.CorrelationId);

            if (WritePropertyName(writer, nameof(IEventMetadata.Timestamp), properties))
                writer.WriteValue(e.Timestamp.ToExtendedIso());
            
            if (WritePropertyName(writer, nameof(IEventMetadata.LocalId), properties))
                writer.WriteValue(e.LocalId.ToString());
            
            if (WritePropertyName(writer, nameof(IEventMetadata.OriginId), properties))
                writer.WriteValue(e.OriginId.ToString());
            
            if (WritePropertyName(writer, nameof(IEventMetadata.StreamHash), properties))
                writer.WriteValue(e.StreamHash);

            if (WritePropertyName(writer, nameof(IEventMetadata.Timeline), properties))
                writer.WriteValue(e.Timeline);
            
            if (WritePropertyName(writer, nameof(IEventMetadata.MessageType), properties))
                writer.WriteValue(e.GetType().FullName);
            
            if (WritePropertyName(writer, nameof(IEventMetadata.Version), properties))
                writer.WriteValue(e.Version);

            if (e.ContentHash != string.Empty)
            {
                if (WritePropertyName(writer, nameof(IEventMetadata.ContentHash), properties))
                    writer.WriteValue(e.ContentHash);
            }
        }

        private void SerializeEventAndMetadata(IEvent e, out string eventJson, out string metadataJson, IEnumerable<string> ignoredProperties = null)
        {
            eventJson = null;
            metadataJson = null;
            if (e == null)
               return;
            
            var serializer = _serializationRegistry.GetSerializer(e);
            if (serializer == null)
                return;
            
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            var writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented };

            writer.WriteStartObject();
            WriteEventMetadata(writer, e, ignoredProperties);
            
            metadataJson = sb + "\n}";

            if (e.CommandId != default)
            {
                writer.WritePropertyName(nameof(IEvent.CommandId));
                writer.WriteValue(e.CommandId.ToString());
            }
            
            writer.WritePropertyName(nameof(IEvent.Stream));
            writer.WriteValue(e.Stream);
            
            serializer.Write(writer, e);
            writer.WriteEndObject();
            eventJson = sw.ToString();
        }
        
        private string SerializeEvent(IEvent e)
        {
            if (e == null)
                return null;
            
            var sw = new StringWriter();
            var writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented };
            
            var serializer = _serializationRegistry.GetSerializer(e);
            if (serializer == null)
                return null;

            writer.WriteStartObject();
            
            WriteEventMetadata(writer, e);

            if (e.CommandId != null)
            {
                writer.WritePropertyName(nameof(IEvent.CommandId));
                writer.WriteValue(e.CommandId.ToString());
            }
            
            writer.WritePropertyName(nameof(IEvent.Stream));
            writer.WriteValue(e.Stream);
            
            serializer.Write(writer, e);
            
            writer.WriteEndObject();
            sw.Flush();
            return sw.ToString();
        }
        
        private Event DeserializeEvent(string payload, bool full = true)
        {
            if (payload == null)
                return null;

            var reader = new JsonTextReader(new StringReader(payload))
                { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable };
            if(_jsonArrayPool != null)
                reader.ArrayPool = _jsonArrayPool;

            var deserializer = _serializationRegistry.GetDeserializer(payload);
            if (deserializer == null)
                return null;

            var e = deserializer.Create();
            if (!full)
                return e;
            e.CorrelationId = null;
            
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
                        e.MessageId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(Message.AncestorId):
                        e.AncestorId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(Message.CorrelationId):
                        e.CorrelationId = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(Message.Timestamp):
                        e.Timestamp = Time.FromExtendedIso((string)reader.Value);
                        break;
                    case JsonToken.Date when currentProperty == nameof(Message.Timestamp):
                        e.Timestamp = Time.FromExtendedIso(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(Message.LocalId):
                        e.LocalId = EventId.Parse((string)reader.Value); 
                        break;
                    case JsonToken.String when currentProperty == nameof(Message.OriginId):
                        e.OriginId = EventId.Parse((string)reader.Value); 
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.StreamHash):
                        e.StreamHash = (string)reader.Value;
                        break;
                    case JsonToken.Integer when currentProperty == nameof(EventMetadata.Version):
                        e.Version = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.MessageType):
                        e.MessageType = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.ContentHash):
                        e.ContentHash = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(Event.CommandId):
                        e.CommandId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(Event.Stream):
                        e.Stream = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(Event.Timeline):
                        e.Timeline = (string)reader.Value;
                        break;
                }
                
                deserializer.Switch(reader, currentProperty, e);
            }

            return e;
        }
        #endif
        
    }
}
