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
        private MessageId _messageId;

        /// <summary>
        /// Message constuctor
        /// </summary>
        public Message()
        {
            MessageType = GetType().Name;
        }
        
        /// <inheritdoc />
        public MessageId MessageId
        {
            get
            {
                if (_messageId == default)
                    _messageId = new MessageId(MessageType, Guid.NewGuid());
                return _messageId;
            }
            set => _messageId = value;
        }

        /// <inheritdoc />
        public string MessageType { get; set; }
        
        /// <inheritdoc />
        public MessageId AncestorId { get; set; }

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