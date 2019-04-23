using System;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class Event : IEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        protected Event()
        {
            EventType = GetType().Name;
            EventId = Guid.NewGuid();
        }

        /// <inheritdoc />
        public Guid EventId { get; }

        /// <inheritdoc />
        public string EventType { get; }

        /// <inheritdoc />
        public long Timestamp { get; set; }

        /// <inheritdoc />
        public int Version { get; set; }

        /// <inheritdoc />
        public string Stream { get; set; }
    }
}