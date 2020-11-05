namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Projection manager
    /// </summary>
    public interface IProjectionManager
    {
        /// <summary>
        /// Get the projection instance
        /// </summary>
        /// <param name="id">Stream id</param>
        /// <param name="timeline">Timeline to query</param>
        /// <typeparam name="TState">State type</typeparam>
        /// <returns>Projection instance</returns>
        IProjection<TState> GetProjection<TState>(string id = "", string timeline = "")
            where TState : IState;

        /// <summary>
        /// Get the historical projection instance
        /// </summary>
        /// <param name="id">Stream id</param>
        /// <typeparam name="TState">State type</typeparam>
        /// <returns>Historical projection instance</returns>
        IHistoricalProjection<TState> GetHistoricalProjection<TState>(string id = "")
            where TState : IState, new();
    }
}