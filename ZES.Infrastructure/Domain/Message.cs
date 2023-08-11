using System;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;

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
        public Time Timestamp { get; set; }

        /// <inheritdoc />
        public string Timeline { get; set; }

        /// <inheritdoc />
        public EventId LocalId { get; set; }

        /// <inheritdoc />
        public EventId OriginId { get; set; }

        /// <inheritdoc />
        public string Json { get; set; }

        /// <inheritdoc />
        public string JsonMetadata { get; set; }
    }
}