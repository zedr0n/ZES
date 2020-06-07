namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Single stream query
    /// </summary>
    public interface ISingleQuery
    {
        /// <summary>
        /// Gets the id of the underlying stream
        /// </summary>
        string Id { get; }
    }
}