using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="IEvent" />
    public class EventMetadata : Message, IEventMetadata
    {
        /// <inheritdoc />
        public string MessageType { get; set; }

        /// <inheritdoc />
        public int Version { get; set; }
    }
}