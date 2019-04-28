using System;

namespace ZES.Interfaces
{
    /// <summary>
    /// 
    /// </summary>
    public interface IEventMetadata : IMessage
    {
        /// <summary>
        /// Gets event version in appropriate stream
        /// </summary>
        /// <value>
        /// Event version in appropriate stream
        /// </value>
        int Version { get; }
        
        /// <summary>
        /// Gets event unix epoch timestamp
        /// </summary>
        /// <value>
        /// Event unix epoch timestamp
        /// </value>
        new long Timestamp { get; set; }
    }

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