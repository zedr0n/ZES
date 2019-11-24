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
        /// Gets a value indicating whether the message is idempotent
        /// </summary>
        /// <value>
        /// A value indicating whether the message is idempotent
        /// </value>
        bool Idempotent { get; }

        /// <summary>
        /// Gets event unix epoch timestamp
        /// </summary>
        /// <value>
        /// Event unix epoch timestamp
        /// </value>
        long Timestamp { get; }
    }
}