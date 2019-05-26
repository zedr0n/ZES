using System;

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
        /// Gets .Global position in event store
        /// </summary>
        /// <value>
        /// Global position in event store
        /// </value>
        long Position { get; }
        
        /// <summary>
        /// Gets event unix epoch timestamp
        /// </summary>
        /// <value>
        /// Event unix epoch timestamp
        /// </value>
        long Timestamp { get; }
    }
}