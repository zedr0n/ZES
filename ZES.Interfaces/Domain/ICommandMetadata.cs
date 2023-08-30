namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Base command metadata interface
    /// </summary>
    public interface ICommandMetadata : IMessageMetadata
    {
        /// <summary>
        /// Create a copy of metadata
        /// </summary>
        /// <returns>Metadata copy</returns>
        ICommandMetadata Copy();
    }    
}
