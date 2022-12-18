namespace ZES.Interfaces
{
    /// <summary>
    /// Event metadata without content 
    /// </summary>
    public interface IEventMetadata : IMessage
    {
        /// <summary>
        /// Gets event type
        /// </summary>
        string MessageType { get; }
        
        /// <summary>
        /// Gets or sets gets event version in appropriate stream
        /// </summary>
        int Version { get; set; }

        /// <summary>
        /// Gets or sets the stream hash
        /// </summary>
        string StreamHash { get; set; }
        
        /// <summary>
        /// Gets or sets the aggregate content hash
        /// </summary>
        string ContentHash { get; set; }
    }
}