using System;

namespace ZES.Interfaces
{
    /// <summary>
    /// Base event
    /// </summary>
    public interface IEvent : IEventMetadata
    {
        /// <summary>
        /// Gets event type
        /// </summary>
        /// <value>
        /// Event type
        /// </value>
        string EventType { get; }
        
        /// <summary>
        /// Gets originating command id
        /// </summary>
        Guid CommandId { get; }

        /// <summary>
        /// Gets or sets gets originating stream key
        /// </summary>
        /// <value>
        /// Originating stream key
        /// </value>
        string Stream { get; set; }
    }
}