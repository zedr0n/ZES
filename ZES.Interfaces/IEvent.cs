using System;

namespace ZES.Interfaces
{
    public interface IEvent
    {
        /// <summary>
        /// Unique event id
        /// </summary>
        Guid EventId { get; }
        /// <summary>
        /// Event type
        /// </summary>
        string EventType { get; }
        /// <summary>
        /// Event Unix epoch timestamp
        /// </summary>
        long Timestamp { get; } 
        /// <summary>
        /// Event version in appropriate stream
        /// </summary>
        int Version { get; }
        /// <summary>
        /// Originating stream key
        /// </summary>
        string Stream { get;  }
    }
}