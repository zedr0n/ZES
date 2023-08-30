using ZES.Interfaces.Clocks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Base message metadata interface
    /// </summary>
    public interface IMessageMetadata 
    {
        /// <summary>
        /// Gets or sets unique message identifier
        /// </summary>
        MessageId MessageId { get; set; }
        
        /// <summary>
        /// Gets or sets gets event timestamp
        /// </summary>
        Time Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the corresponding message timeline
        /// </summary>
        string Timeline { get; set; }
        
        /// <summary>
        /// Gets or sets the json serialisation of metadata
        /// </summary>
        string Json { get; set; }
    } 
}
