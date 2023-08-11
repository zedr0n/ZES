using System;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Base message
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets unique message identifier
        /// </summary>
        Guid MessageId { get; }

        /// <summary>
        /// Gets id of a message in a causality relationship with this message
        /// </summary>
        Guid AncestorId { get; }

        /// <summary>
        /// Gets or sets the correlation id for the message
        /// </summary>
        string CorrelationId { get; set; }
        
        /// <summary>
        /// Gets or sets gets event timestamp
        /// </summary>
        Time Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the corresponding message timeline
        /// </summary>
        string Timeline { get; set; }
        
        /// <summary>
        /// Gets or sets the message local id
        /// </summary>
        EventId LocalId { get; set; }
        
        /// <summary>
        /// Gets or sets the message origin id
        /// </summary>
        EventId OriginId { get; set; }
        
        /// <summary>
        /// Gets or sets the serialized json
        /// </summary>
        string Json { get; set; }

        /// <summary>
        /// Gets or sets the serialized json metadata
        /// </summary>
        string JsonMetadata { get; set; }
    }
}