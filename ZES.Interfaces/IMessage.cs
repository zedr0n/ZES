namespace ZES.Interfaces
{
    /// <summary>
    /// Base message
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets event unix epoch timestamp
        /// </summary>
        /// <value>
        /// Event unix epoch timestamp
        /// </value>
        long? Timestamp { get; }
    }
}