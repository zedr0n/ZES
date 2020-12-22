namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Single stream query
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public interface ISingleQuery<TState> : IQuery<TState>
    {
        /// <summary>
        /// Gets the id of the underlying stream
        /// </summary>
        string Id { get; }
    }
}