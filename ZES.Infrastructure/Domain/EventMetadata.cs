using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="IEvent" />
    public class EventMetadata : Message, IEventMetadata
    {
        /// <inheritdoc />
        public int Version { get; set; }
    }
}