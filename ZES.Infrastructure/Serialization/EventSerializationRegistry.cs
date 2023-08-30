using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public class EventSerializationRegistry : IEventSerializationRegistry
    {
        private readonly IEnumerable<IEventDeserializer> _deserializers;
        private readonly IEnumerable<IEventSerializer> _serializers;
        private readonly Dictionary<int, IEventDeserializer> _eventDeserializers = new();

        private readonly string _startToken = "{" + Environment.NewLine + "  \"$type\": \"";
        
        /// <summary>
        /// Initializes a new instance of the <see cref="EventSerializationRegistry"/> class.
        /// </summary>
        /// <param name="deserializers">Registered deserializers</param>
        /// <param name="serializers">Registered serializers</param>
        public EventSerializationRegistry(IEnumerable<IEventDeserializer> deserializers, IEnumerable<IEventSerializer> serializers)
        {
            _deserializers = deserializers;
            _serializers = serializers;
            foreach (var d in _deserializers)
                _eventDeserializers[d.EventType.GetHashCode()] = d;
        }

        /// <inheritdoc />
        public IEventDeserializer GetDeserializer(string payload)
        {
            var type = GetTypeHashCodeFromPayload(payload); 
            if (type != null && _eventDeserializers.TryGetValue(type.Value, out var deserializer))
                return deserializer;
            else
                return _deserializers.FirstOrDefault(d => payload.StartsWith(_startToken + d.EventType));
        }

        /// <inheritdoc />
        public IEventSerializer GetSerializer(IEvent e)
        {
            return _serializers.FirstOrDefault(x => x.EventType == e.GetType().FullName);
        }

        private int? GetTypeHashCodeFromPayload(string payload)
        {
            using var reader = new JsonTextReader(new StringReader(payload));
            while (reader.TokenType != JsonToken.String)
                reader.Read();

            var type = reader.Value?.ToString();
            return type?.GetHashCode();
        }
    }
}