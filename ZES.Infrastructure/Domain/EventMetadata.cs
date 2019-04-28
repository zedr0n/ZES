using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class EventMetadata : IEventMetadata
    {
        /// <inheritdoc />
        public int Version { get; set; } 
        
        /// <inheritdoc />
        public long Timestamp { get; set; }

        long? IMessage.Timestamp => Timestamp;
    }
}