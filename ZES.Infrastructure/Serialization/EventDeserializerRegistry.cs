using System.Collections.Generic;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public class EventDeserializerRegistry : IEventDeserializerRegistry
    {
        private readonly IEnumerable<IEventDeserializer> _deserializers;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventDeserializerRegistry"/> class.
        /// </summary>
        /// <param name="deserializers">Registered deserializers</param>
        public EventDeserializerRegistry(IEnumerable<IEventDeserializer> deserializers)
        {
            _deserializers = deserializers;
        }

        /// <inheritdoc />
        public IEventDeserializer GetDeserializer(string payload)
        {
            foreach (var d in _deserializers)
            {
                if (payload.Contains(d.EventType))
                    return d;
            }

            return null;
        }
    }
}