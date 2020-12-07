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
        /// <value>
        /// Unique message identifier
        /// </value>
        Guid MessageId { get; }

        /// <summary>
        /// Gets id of a message in a causality relationship with this message
        /// </summary>
        /// <value>
        /// Id of a message in a causality relationship with this message
        /// </value>
        Guid AncestorId { get; }

        /// <summary>
        /// Gets or sets gets event unix epoch timestamp
        /// </summary>
        /// <value>
        /// Event unix epoch timestamp
        /// </value>
        Instant Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the corresponding message timeline
        /// </summary>
        string Timeline { get; set; }
    }
}