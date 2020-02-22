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
        /// <value>
        /// Event type of the message
        /// </value>
        string MessageType { get; }
        
        /// <summary>
        /// Gets or sets gets event version in appropriate stream
        /// </summary>
        /// <value>
        /// Event version in appropriate stream
        /// </value>
        int Version { get; set; }

        /// <summary>
        /// Gets or sets the event hash
        /// </summary>
        string Hash { get; set; }
    }
}