using System;

namespace ZES.Interfaces
{
    /// <summary>
    /// Base event
    /// </summary>
    public interface IEvent : IEventMetadata
    {
        /// <summary>
        /// Gets unique event id
        /// </summary>
        /// <value>
        /// Unique event id
        /// </value>
        Guid EventId { get; }

        /// <summary>
        /// Gets event type
        /// </summary>
        /// <value>
        /// Event type
        /// </value>
        string EventType { get; }

        /// <summary>
        /// Gets originating stream key
        /// </summary>
        /// <value>
        /// Originating stream key
        /// </value>
        string Stream { get;  }
    }
}