namespace ZES.Interfaces
{
    /// <summary>
    /// Event metadata without content 
    /// </summary>
    public interface IEventMetadata : IMessage
    {
        /// <summary>
        /// Gets event version in appropriate stream
        /// </summary>
        /// <value>
        /// Event version in appropriate stream
        /// </value>
        int Version { get; }
        
        /// <summary>
        /// Gets or sets event unix epoch timestamp
        /// </summary>
        /// <value>
        /// Event unix epoch timestamp
        /// </value>
        new long Timestamp { get; set; }
    }
}