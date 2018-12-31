using System;

namespace ZES.Interfaces
{
    public interface IEvent
    {
        /// <summary>
        /// Unique event id
        /// </summary>
        Guid EventId { get; set; }
        /// <summary>
        /// Event type
        /// </summary>
        string EventType { get; set; }
        /// <summary>
        /// Event Unix epoch timestamp
        /// </summary>
        long Timestamp { get; set; } 
        /// <summary>
        /// Event version in appropriate stream
        /// </summary>
        int Version { get; set; }
    }
}