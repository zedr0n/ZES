using System;
using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Base message class
    /// </summary>
    public abstract class Message : IMessage
    {
        /// <inheritdoc />
        public Guid MessageId { get; set; }

        /// <inheritdoc />
        public Guid AncestorId { get; set; }

        /// <inheritdoc />
        public bool Idempotent
        {
            get => IdempotentImpl;
            set => IdempotentImpl = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public long Position { get; set; }

        /// <inheritdoc />
        public long Timestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether protected member defaulting idempotency
        /// </summary>
        /// <value>
        /// A value indicating whether protected member defaulting idempotency
        /// </value>
        protected virtual bool IdempotentImpl { get; set; } = false;
    }
}