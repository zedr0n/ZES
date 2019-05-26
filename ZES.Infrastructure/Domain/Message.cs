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
        [JsonIgnore]
        public long Position { get; set; }

        /// <inheritdoc />
        public long Timestamp { get; set; }
    }
}