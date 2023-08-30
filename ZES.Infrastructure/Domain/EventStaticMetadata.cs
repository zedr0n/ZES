using System;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Event static metadata
    /// </summary>
    public class EventStaticMetadata : MessageStaticMetadata, IEventStaticMetadata
    {
        /// <inheritdoc />
        public string OriginatingStream { get; set; }
        
        /// <inheritdoc />
        public MessageId CommandId { get; set; }
        
        /// <inheritdoc />
        public IEventStaticMetadata Copy()
        {
            var copy = MemberwiseClone() as EventStaticMetadata;
            return copy;
        }

    }    
}

