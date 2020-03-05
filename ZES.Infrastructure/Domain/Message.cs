using System;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Base message class
    /// </summary>
    public abstract class Message : IMessage
    {
        private Guid _messageId;

        /// <inheritdoc />
        public Guid MessageId
        {
            get
            {
                if (_messageId == default)
                    _messageId = Guid.NewGuid();
                return _messageId;
            }
            set => _messageId = value;
        }

        /// <inheritdoc />
        public Guid AncestorId { get; set; }

        /// <inheritdoc />
        public long Timestamp { get; set; }
    }
}