using System;
using NodaTime;

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
        /// Gets or sets gets event timestamp
        /// </summary>
        Instant Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the corresponding message timeline
        /// </summary>
        string Timeline { get; set; }
    }
}