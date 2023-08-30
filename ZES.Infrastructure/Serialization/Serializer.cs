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
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;
using Stream = ZES.Infrastructure.EventStore.Stream;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public class JsonArrayPool : IArrayPool<char>
    {
        /// <summary>
        /// Json array pool instance
        /// </summary>
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
                NullValueHandling = NullValueHandling.Ignore,

                // In a version resilient way
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                Converters = new List<JsonConverter>(),
                DateParseHandling = DateParseHandling.None,
            }).ConfigureForTime(); 
            JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                NullValueHandling = NullValueHandling.Ignore,

                // In a version resilient way
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
        public T Deserialize(string payload, string metadata, SerializationType serializationType = SerializationType.PayloadAndMetadata)
        {
            T result = null;
            _log.StopWatch.Start(nameof(Deserialize));
            using var reader = new StringReader(payload);
            try
            {
#if USE_EXPLICIT
                result = DeserializeEvent(payload, metadata, out var jsonMetadata, out var jsonStaticMetadata, serializationType) as T;
#endif
                if (result == null)
                {
                    var jsonReader = new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable};
                    if(_jsonArrayPool != null)
                        jsonReader.ArrayPool = _jsonArrayPool;
                        
                    result = _serializer.Deserialize<T>(jsonReader);
                    if (result == null)
                        throw new JsonSerializationException($"Couldn't deserialize event: {payload}");

                    if (result is IMessage message)
                    {
                        if (Configuration.StoreMetadataSeparately )
                        {
                            using var metadataReader = new StringReader(metadata);
                            var jsonMetadataReader = new JsonTextReader(metadataReader) { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable};
                            if(_jsonArrayPool != null)
                                jsonMetadataReader.ArrayPool = _jsonArrayPool;
                            var metadataEvent = _serializer.Deserialize<T>(jsonMetadataReader) as IMessage;
                            message.CopyMetadata(metadataEvent);
                        }
                        
                        message.Json = payload;
                        message.MetadataJson = jsonMetadata;
                        message.StaticMetadataJson = jsonStaticMetadata;
                        
                        //_log.Warn($"Used generic serializer for {message.MessageId} of type {typeof(T)}");
                    }
                }
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
                    jsonWriter.WriteValue(stream.SnapshotTimestamp.Serialise());
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
                        jsonWriter.WriteValue(stream.Parent.SnapshotTimestamp.Serialise());
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
                        snapshotTimestamp = Time.Parse((string)reader.Value);
                        break;
                    case JsonToken.Integer when currentProperty == $"Parent{nameof(IStream.SnapshotVersion)}":
                        parentSnapshotVersion = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == $"Parent{nameof(IStream.SnapshotTimestamp)}":
                        parentSnapshotTimestamp = Time.Parse((string)reader.Value); 
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
#if USE_EXPLICIT
            
            var sw = new StringWriter();
            var writer = new JsonTextWriter(sw);

            writer.WriteStartObject();
            
            writer.WritePropertyName("$type");
            writer.WriteValue(e.GetType().FullName + "," + e.GetType().Assembly.FullName.Split(',')[0]);
            
            writer.WritePropertyName(nameof(Event.Metadata));
            writer.WriteStartObject();
            {
                WriteEventMetadata(writer, e.Metadata);
            }
            writer.WriteEndObject();

            if (!Configuration.StoreMetadataSeparately)
            {
                writer.WritePropertyName(nameof(Event.StaticMetadata));
                writer.WriteStartObject();
                {
                    WriteEventStaticMetadata(writer, e.StaticMetadata);
                }
                writer.WriteEndObject();
            }
            
            writer.WriteEndObject();
            
            return sw.ToString();
#else
            var meta = new JObject(
                new JProperty(nameof(IEventMetadata.MessageId), e.MessageId),
                new JProperty(nameof(IEventMetadata.AncestorId), e.AncestorId),
                new JProperty(nameof(IEventMetadata.Timestamp), e.Timestamp.Serialise()),
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
        public IEvent DecodeMetadata(string json)
        {
            _log.StopWatch.Start(nameof(DecodeMetadata));
#if USE_EXPLICIT
            var reader = new JsonTextReader(new StringReader(json))
                { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable };
            if(_jsonArrayPool != null)
                reader.ArrayPool = _jsonArrayPool;
            
            var metadata = new Event();
            var currentProperty = string.Empty;
            // metadata.CorrelationId = null;
            while (reader.Read())
            {
                if (reader.Value == null) 
                    continue;
                
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        currentProperty = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventStaticMetadata.AncestorId):
                        metadata.StaticMetadata.AncestorId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventStaticMetadata.CorrelationId):
                        metadata.StaticMetadata.CorrelationId = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.MessageId):
                        metadata.Metadata.MessageId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.Timestamp):
                        metadata.Metadata.Timestamp = Time.Parse((string)reader.Value);
                        break;
                    case JsonToken.Integer when currentProperty == nameof(IEventMetadata.Version):
                        metadata.Metadata.Version = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventStaticMetadata.MessageType):
                        metadata.StaticMetadata.MessageType = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.ContentHash):
                        metadata.Metadata.ContentHash = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.StreamHash):
                        metadata.Metadata.StreamHash = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(IEventMetadata.Timeline):
                        metadata.Metadata.Timeline = (string)reader.Value;
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

        private void WriteMessageMetadata(JsonWriter writer, IMessageMetadata e, IEnumerable<string> ignoredProperties = null)
        {
            var properties = ignoredProperties as string[] ?? ignoredProperties?.ToArray();
            
            writer.WritePropertyName("$type");
            writer.WriteValue(e.GetType().FullName + "," + e.GetType().Assembly.FullName.Split(',')[0]);
            
            if (WritePropertyName(writer, nameof(IMessageMetadata.MessageId), properties))
                writer.WriteValue(e.MessageId.ToString());

            if (WritePropertyName(writer, nameof(IMessageMetadata.Timestamp), properties))
                writer.WriteValue(e.Timestamp.Serialise());
            
            if (WritePropertyName(writer, nameof(IMessageMetadata.Timeline), properties))
                writer.WriteValue(e.Timeline);
        }
        
        private void WriteEventMetadata(JsonWriter writer, IEventMetadata e, IEnumerable<string> ignoredProperties = null)
        {
            var properties = ignoredProperties as string[] ?? ignoredProperties?.ToArray();

            WriteMessageMetadata(writer, e, properties);
            
            if (WritePropertyName(writer, nameof(IEventMetadata.Version), properties))
                writer.WriteValue(e.Version);
            
            if (WritePropertyName(writer,nameof(IEventMetadata.Stream), properties))
                writer.WriteValue(e.Stream);
            
            if (e.ContentHash != string.Empty)
            {
                if (WritePropertyName(writer, nameof(IEventMetadata.ContentHash), properties))
                    writer.WriteValue(e.ContentHash);
            }
            
            if (WritePropertyName(writer, nameof(IEventMetadata.StreamHash), properties))
                writer.WriteValue(e.StreamHash);
        }

        private void WriteMessageStaticMetadata(JsonWriter writer, IMessageStaticMetadata e, IEnumerable<string> ignoredProperties = null)
        {
            var properties = ignoredProperties as string[] ?? ignoredProperties?.ToArray();
            
            writer.WritePropertyName("$type");
            writer.WriteValue(e.GetType().FullName + "," + e.GetType().Assembly.FullName.Split(',')[0]);
            
            if (WritePropertyName(writer, nameof(IMessageStaticMetadata.MessageType), properties))
                writer.WriteValue(e.MessageType);
            
            if (e.AncestorId != default && WritePropertyName(writer, nameof(IMessageStaticMetadata.AncestorId), properties))
                writer.WriteValue(e.AncestorId.ToString());

            if (WritePropertyName(writer, nameof(IMessageStaticMetadata.CorrelationId), properties))
                writer.WriteValue(e.CorrelationId);

            if (WritePropertyName(writer, nameof(IMessageStaticMetadata.LocalId), properties))
                writer.WriteValue(e.LocalId.ToString());
            
            if (/*e.OriginId != e.LocalId &&*/ WritePropertyName(writer, nameof(IMessageStaticMetadata.OriginId), properties))
                writer.WriteValue(e.OriginId.ToString());
        }
        
        private void WriteEventStaticMetadata(JsonWriter writer, IEventStaticMetadata e, IEnumerable<string> ignoredProperties = null)
        {
            var properties = ignoredProperties as string[] ?? ignoredProperties?.ToArray();

            WriteMessageStaticMetadata(writer, e, properties);
            
            if(WritePropertyName(writer, nameof(IEventStaticMetadata.OriginatingStream), properties))
                writer.WriteValue(e.OriginatingStream);
            
            if(e.CommandId != default && WritePropertyName(writer, nameof(IEventStaticMetadata.CommandId), properties))
                writer.WriteValue(e.CommandId.ToString());
        }

        private void SerializeEventAndMetadata(IEvent e, out string eventJson, out string metadataJson, IEnumerable<string> ignoredProperties = null)
        {
            eventJson = null;
            metadataJson = null;
            if (e == null)
               return;
            
            var serializer = _serializationRegistry.GetSerializer(e);

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            var writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented };
            
            var metadataSb = new StringBuilder();
            var swMetadata = new StringWriter(metadataSb);
            var metadataWriter = new JsonTextWriter(swMetadata) { Formatting = Formatting.Indented };
            
            var properties = ignoredProperties as string[] ?? ignoredProperties?.ToArray();
            
            writer.WriteStartObject();
            
            writer.WritePropertyName("$type");
            writer.WriteValue(e.GetType().FullName + "," + e.GetType().Assembly.FullName.Split(',')[0]);
            if (Configuration.StoreMetadataSeparately)
            {
               metadataWriter.WriteStartObject();
               metadataWriter.WritePropertyName("$type");
               metadataWriter.WriteValue(e.GetType().FullName + "," + e.GetType().Assembly.FullName.Split(',')[0]);
            }
            
            var currentWriter = Configuration.StoreMetadataSeparately ? metadataWriter : writer;
            currentWriter.WritePropertyName(nameof(Event.Metadata));
            currentWriter.WriteStartObject();
            {
                WriteEventMetadata(currentWriter, e.Metadata, properties);
            }
            currentWriter.WriteEndObject();

            if (Configuration.StoreMetadataSeparately)
            {
                metadataWriter.WriteEndObject();
                metadataJson = metadataSb.ToString();
            }

            writer.WritePropertyName(nameof(Event.StaticMetadata));
            writer.WriteStartObject();
            {
                WriteEventStaticMetadata(writer, e.StaticMetadata, properties);
            }
            writer.WriteEndObject();

            if (!Configuration.StoreMetadataSeparately)
                metadataJson = sb + Environment.NewLine + "}";

            if(serializer != null)
                serializer.Write(writer, e);
            else
            {
                var payloadSb = new StringBuilder();
                using var payloadWriter = new StringWriter(payloadSb);
                var jsonWriter = new JsonTextWriter(payloadWriter) { Formatting = Formatting.Indented };

                var copy = e.CopyPayload();
                _serializer.Serialize(jsonWriter, copy);
                var payloadJson = payloadSb.ToString();
                sw.Write(',');
                if (payloadJson.Length > 2)
                {
                    // remove {}
                    payloadJson = payloadJson.Substring(1, payloadJson.Length - 2);
                    sw.Write(payloadJson);
                }
            }
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
            writer.WritePropertyName("$type");
            writer.WriteValue(e.GetType().FullName);
            
            if(!Configuration.StoreMetadataSeparately)
                WriteEventMetadata(writer, e.Metadata);
            WriteEventStaticMetadata(writer, e.StaticMetadata);
            
            serializer.Write(writer, e);
            
            writer.WriteEndObject();
            sw.Flush();
            return sw.ToString();
        }

        private EventMetadata DeserializeEventMetadata(JsonReader reader)
        {
            var e = new EventMetadata();
            var currentProperty = string.Empty;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    return e;

                if (reader.Value == null)
                    continue;

                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        currentProperty = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.MessageId):
                        e.MessageId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.Timestamp):
                        e.Timestamp = Time.Parse((string)reader.Value);
                        break;
                    case JsonToken.Date when currentProperty == nameof(EventMetadata.Timestamp):
                        e.Timestamp = Time.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.Timeline):
                        e.Timeline = (string)reader.Value;
                        break;
                    case JsonToken.Integer when currentProperty == nameof(EventMetadata.Version):
                        e.Version = (int)(long)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.Stream):
                        e.Stream = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.StreamHash):
                        e.StreamHash = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(EventMetadata.ContentHash):
                        e.ContentHash = (string)reader.Value;
                        break;
                }
            }

            return e;
        }

        private EventStaticMetadata DeserializeEventStaticMetadata(JsonReader reader)
        {
            var e = new EventStaticMetadata();
            var currentProperty = string.Empty;
            
            using var writer = new StringWriter();

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    return e;

                if(reader.Value == null)
                    continue;

                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        currentProperty = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(EventStaticMetadata.AncestorId):
                        e.AncestorId = MessageId.Parse(reader.Value.ToString());
                        break;
                    case JsonToken.String when currentProperty == nameof(EventStaticMetadata.CorrelationId):
                        e.CorrelationId = reader.Value.ToString();
                        break;
                    case JsonToken.String when currentProperty == nameof(EventStaticMetadata.LocalId):
                        e.LocalId = EventId.Parse((string)reader.Value); 
                        break;
                    case JsonToken.String when currentProperty == nameof(EventStaticMetadata.OriginId):
                        e.OriginId = EventId.Parse((string)reader.Value); 
                        break;
                    case JsonToken.String when currentProperty == nameof(EventStaticMetadata.MessageType):
                        e.MessageType = (string)reader.Value;
                        break;
                    case JsonToken.String when currentProperty == nameof(EventStaticMetadata.CommandId):
                        e.CommandId = MessageId.Parse(reader.Value.ToString());
                        break;
                } 
            }

            e.OriginId ??= e.LocalId;

            return null;
        }

        private static bool TryGetJson(string payload, string metadata, out string jsonMetadata, out string jsonStaticMetadata, out string eventPayload)
        {
            jsonMetadata = jsonStaticMetadata = eventPayload = null;
            var split = Configuration.StoreMetadataSeparately ? nameof(Event.StaticMetadata) : nameof(Event.Metadata);
            var tokens = payload.Split(new[] { $"{split}\": ", "}," }, StringSplitOptions.None);
            if (Configuration.StoreMetadataSeparately)
            {
                if (tokens.Length < 3)
                    return false;
                jsonMetadata = metadata;
                jsonStaticMetadata = tokens[1] + "}";
                if (tokens.Length > 2)
                    eventPayload = "{" + Environment.NewLine + tokens[2];
            }
            else
            {
                if (tokens.Length < 4)
                    return false;
                jsonMetadata = tokens[1] + Environment.NewLine + "}";
                jsonStaticMetadata = tokens[3] + "}";
                if (tokens.Length > 4)
                    eventPayload = "{" + Environment.NewLine + tokens[4];
            }
            return true;
        }
        
        private Event DeserializeEvent(string payload, string metadata, out string jsonMetadata, out string jsonStaticMetadata, SerializationType serializationType = SerializationType.PayloadAndMetadata )
        {
            jsonMetadata = jsonStaticMetadata = null;
            if (payload == null || metadata == null)
                return null;

            if (!TryGetJson(payload, metadata, out jsonMetadata, out jsonStaticMetadata, out var eventPayload))
                throw new SerializationException($"Cannot extract json : {payload}");
            
            var reader = new JsonTextReader(new StringReader(payload))
                { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable };
            if(_jsonArrayPool != null)
                reader.ArrayPool = _jsonArrayPool;

            var metadataReader = new JsonTextReader(new StringReader(jsonMetadata)) 
                { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable };
            if(_jsonArrayPool != null)
                metadataReader.ArrayPool = _jsonArrayPool;

            var deserializer = _serializationRegistry.GetDeserializer(payload);
            if (deserializer == null)
                return null;
            var e = deserializer.Create();
            e.Json = payload;
            
            var currentProperty = string.Empty;
            
            if (!deserializer.SupportsPayload && serializationType == SerializationType.PayloadAndMetadata)
                return null;

            e.StaticMetadata = new EventStaticMetadata();
            e.StaticMetadata.Json = jsonStaticMetadata;
            
            if (Configuration.StoreMetadataSeparately)
            {
                e.Metadata = DeserializeEventMetadata(metadataReader);
                e.Metadata.Json = jsonMetadata;
            }
            
            while (reader.Read())
            {
                if (reader.Value == null)
                    continue;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;
                
                currentProperty = reader.Value.ToString();
                if (currentProperty == nameof(Event.Metadata))
                {
                    e.Metadata = DeserializeEventMetadata(reader);
                    e.Metadata.Json = jsonMetadata;
                }
                if (currentProperty == nameof(Event.StaticMetadata) && serializationType != SerializationType.Metadata)
                {
                    e.StaticMetadata = DeserializeEventStaticMetadata(reader);
                    break;
                }
            }

            if (serializationType is SerializationType.FullMetadata or SerializationType.Metadata)
                return e;

            reader = new JsonTextReader(new StringReader(eventPayload))
                { DateParseHandling = DateParseHandling.None, PropertyNameTable = _jsonNameTable };
            if(_jsonArrayPool != null)
                reader.ArrayPool = _jsonArrayPool;
            
            while (reader.Read())
            {
                if (reader.Value == null)
                    continue;

                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        currentProperty = reader.Value.ToString();
                        break;
                    default:
                        deserializer.Switch(reader, currentProperty, e);
                        break;
                } 
            }
            return e;
        }
        #endif
        
    }
}
