using System;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Base message interface 
    /// </summary>
    public interface IMessage
    {
        /// <inheritdoc cref="IMessageMetadata.MessageId"/>
        MessageId MessageId { get; }
        
        /// <inheritdoc cref="IMessageStaticMetadata.MessageType"/>
        string MessageType { get; }
        
        /// <inheritdoc cref="IMessageStaticMetadata.AncestorId"/>
        MessageId AncestorId { get; set; }
        
        /// <inheritdoc cref="IMessageStaticMetadata.CorrelationId"/>
        string CorrelationId { get; set; }
        
        /// <inheritdoc cref="IMessageStaticMetadata.LocalId"/>
        EventId LocalId { get; set; }
        
        /// <inheritdoc cref="IMessageStaticMetadata.OriginId"/>
        EventId OriginId { get; set; }

        /// <inheritdoc cref="IMessageMetadata.Timestamp"/>
        Time Timestamp { get; set; }
        
        /// <inheritdoc cref="IMessageMetadata.Timeline"/>
        string Timeline { get; set; }
        
        /// <summary>
        /// Gets or sets the serialised json
        /// </summary>
        public string Json { get; set; }
        
        /// <summary>
        /// Gets or sets metadata json
        /// </summary>
        string MetadataJson { get; set; }
        
        /// <summary>
        /// Gets or sets static metadata json
        /// </summary>
        string StaticMetadataJson { get; set; }

        /// <summary>
        /// Copies the metadata from other message
        /// </summary>
        /// <param name="other">Other message</param>
        void CopyMetadata(IMessage other);
    }
    
    /// <summary>
    /// Base message interface 
    /// </summary>
    /// <typeparam name="TStaticMetadata">Static metadata type</typeparam>
    /// <typeparam name="TMetadata">Mutable metadata type</typeparam>
    public interface IMessageEx<out TStaticMetadata, out TMetadata> : IMessage
        where TStaticMetadata : IMessageStaticMetadata
        where TMetadata : IMessageMetadata
    {
        /// <summary>
        /// Gets the mutable metadata
        /// </summary>
        TMetadata Metadata { get; }
        
        /// <summary>
        /// Gets the static metadata
        /// </summary>
        TStaticMetadata StaticMetadata { get; }
    }

    /// <summary>
    /// Base message interface 
    /// </summary>
    /// <typeparam name="TStaticMetadata">Static metadata type</typeparam>
    /// <typeparam name="TMetadata">Mutable metadata type</typeparam>
    /// <typeparam name="TPayload">Payload type</typeparam>
    public interface IMessageEx<out TStaticMetadata, out TMetadata, TPayload> : IMessageEx<TStaticMetadata, TMetadata>
        where TStaticMetadata : IMessageStaticMetadata
        where TMetadata : IMessageMetadata
    {
        /// <summary>
        /// Gets or sets the payload
        /// </summary>
        TPayload Payload { get; set; }
    }
}