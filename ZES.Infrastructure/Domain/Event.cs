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
        protected Event()
        {
            EventType = GetType().Name;
            MessageId = Guid.NewGuid();
        }

        public Event Copy()
        {
            var copy = MemberwiseClone() as Event;
            copy.MessageId = Guid.NewGuid();
            return copy;
        }

        /// <inheritdoc />
        public string EventType { get; set; }

        /// <inheritdoc />
        public string Stream { get; set; }
    }
}