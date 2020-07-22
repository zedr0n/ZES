using System.Collections.Generic;
using System.Linq;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public class EventSerializationRegistry : IEventSerializationRegistry
    {
        private readonly IEnumerable<IEventDeserializer> _deserializers;
        private readonly IEnumerable<IEventSerializer> _serializers;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSerializationRegistry"/> class.
        /// </summary>
        /// <param name="deserializers">Registered deserializers</param>
        /// <param name="serializers">Registered serializers</param>
        public EventSerializationRegistry(IEnumerable<IEventDeserializer> deserializers, IEnumerable<IEventSerializer> serializers)
        {
            _deserializers = deserializers;
            _serializers = serializers;
        }

        /// <inheritdoc />
        public IEventDeserializer GetDeserializer(string payload)
        {
            return _deserializers.FirstOrDefault(d => payload.Contains(d.EventType));
        }

        /// <inheritdoc />
        public IEventSerializer GetSerializer(IEvent e)
        {
            return _serializers.FirstOrDefault(x => x.EventType == e.MessageType);
        }
    }
}