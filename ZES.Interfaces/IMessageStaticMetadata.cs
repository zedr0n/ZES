using System;

namespace ZES.Interfaces
{
    /// <summary>
    /// The common message static metadata
    /// </summary>
    public interface IMessageStaticMetadata 
    {
        /// <summary>
        /// Gets or sets message type
        /// </summary>
        string MessageType { get; set; }

        /// <summary>
        /// Gets or sets id of a message in a causality relationship with this message
        /// </summary>
        MessageId AncestorId { get; set; }
 
        /// <summary>
        /// Gets or sets the correlation id for the message
        /// </summary>
        string CorrelationId { get; set; }
        
        /// <summary>
        /// Gets or sets the id of the origin retroactive command if any
        /// </summary>
        MessageId RetroactiveId { get; set; }
        
        /// <summary>
        /// Gets or sets the message local id
        /// </summary>
        EventId LocalId { get; set; }
        
        /// <summary>
        /// Gets or sets the message origin id
        /// </summary>
        EventId OriginId { get; set; }
        
        /// <summary>
        /// Gets or sets the json serialisation of metadata
        /// </summary>
        string Json { get; set; }
    } 
}
