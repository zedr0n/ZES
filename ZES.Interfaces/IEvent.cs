using System;

namespace ZES.Interfaces
{
    /// <summary>
    /// Base event
    /// </summary>
    public interface IEvent
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
        /// Gets event unix epoch timestamp
        /// </summary>
        /// <value>
        /// Event unix epoch timestamp
        /// </value>
        long Timestamp { get; }

        /// <summary>
        /// Gets event version in appropriate stream
        /// </summary>
        /// <value>
        /// Event version in appropriate stream
        /// </value>
        int Version { get; }

        /// <summary>
        /// Gets originating stream key
        /// </summary>
        /// <value>
        /// Originating stream key
        /// </value>
        string Stream { get;  }
    }
}