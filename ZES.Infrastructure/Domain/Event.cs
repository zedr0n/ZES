using System;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="IEvent" />
    public class Event : EventMetadata, IEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        public Event()
        {
            MessageType = GetType().FullName;
        }

        /// <inheritdoc />
        public Guid CommandId { get; set; }

        /// <inheritdoc />
        public string Stream { get; set; }

        /// <inheritdoc />
        public string OriginatingStream { get; set; }

        /// <inheritdoc />
        public IEvent Copy()
        {
            var copy = MemberwiseClone() as Event;
            copy.OriginatingStream = Stream;
            copy.MessageId = Guid.NewGuid();
            copy.AncestorId = MessageId;
            copy.LocalId = LocalId;
            return copy;
        }
    }
}