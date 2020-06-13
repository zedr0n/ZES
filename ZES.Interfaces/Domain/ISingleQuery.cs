namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Single stream query
    /// </summary>
    public interface ISingleQuery<TState> : IQuery<TState>
        where TState : ISingleState
    {
        /// <summary>
        /// Gets the id of the underlying stream
        /// </summary>
        string Id { get; }
    }
}