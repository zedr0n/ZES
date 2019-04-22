using System;

namespace ZES.Interfaces
{
    public interface IEvent
    {
        /// <summary>
        /// Gets unique event id
        /// </summary>
        Guid EventId { get; }
        
        /// <summary>
        /// Gets event type
        /// </summary>
        string EventType { get; }
        
        /// <summary>
        /// Gets event unix epoch timestamp
        /// </summary>
        long Timestamp { get; } 
        
        /// <summary>
        /// Gets event version in appropriate stream
        /// </summary>
        int Version { get; }
        
        /// <summary>
        /// Gets originating stream key
        /// </summary>
        string Stream { get;  }
    }
}