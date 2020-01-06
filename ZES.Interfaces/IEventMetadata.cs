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
        /// Gets event version in appropriate stream
        /// </summary>
        /// <value>
        /// Event version in appropriate stream
        /// </value>
        int Version { get; }
    }
}