namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Base command static metadata interface
    /// </summary>
    public interface ICommandStaticMetadata : IMessageStaticMetadata
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use timestamp for aggregate events
        /// </summary>
        bool UseTimestamp { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to store the command in the log
        /// </summary>
        bool StoreInLog { get; set; }
               
        /// <summary>
        /// Create a copy of metadata
        /// </summary>
        /// <returns>Metadata copy</returns>
        ICommandStaticMetadata Copy();
    } 
}
