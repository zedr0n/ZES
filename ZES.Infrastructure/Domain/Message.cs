using System;
using NodaTime;
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
        public string CorrelationId { get; set; }

        /// <inheritdoc />
        public Instant Timestamp { get; set; }

        /// <inheritdoc />
        public string Timeline { get; set; }
    }
}