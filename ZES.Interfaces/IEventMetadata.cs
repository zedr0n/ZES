using System;

namespace ZES.Interfaces
{
    /// <summary>
    /// Event static metadata
    /// </summary>
    public interface IEventStaticMetadata : IMessageStaticMetadata
    {
        /// <summary>
        /// Gets or sets originating command id
        /// </summary>
        MessageId CommandId { get; set; }
        
        /// <summary>
        /// Gets or sets originating stream key
        /// </summary>
        string OriginatingStream { get; set; }
        
        /// <summary>
        /// Create a copy of metadata
        /// </summary>
        /// <returns>Metadata copy</returns>
        public IEventStaticMetadata Copy();
    }
    
    /// <summary>
    /// Event metadata
    /// </summary>
    public interface IEventMetadata : IMessageMetadata
    {
        /// <summary>
        /// Gets or sets gets event version in appropriate stream
        /// </summary>
        int Version { get; set; }
        
        /// <summary>
        /// Gets or sets stream key
        /// </summary>
        string Stream { get; set; }
        
        /// <summary>
        /// Gets or sets the stream hash
        /// </summary>
        string StreamHash { get; set; }
        
        /// <summary>
        /// Gets or sets the aggregate content hash
        /// </summary>
        string ContentHash { get; set; }
      
        /// <summary>
        /// Create a copy of metadata
        /// </summary>
        /// <returns>Metadata copy</returns>
        public IEventMetadata Copy();
    }
}