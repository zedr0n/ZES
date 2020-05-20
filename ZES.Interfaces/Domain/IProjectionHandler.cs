namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Projection state handler
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public interface IProjectionHandler<TState>
    {
        /// <summary>
        /// Modify state according to event
        /// </summary>
        /// <param name="e">Event to handle</param>
        /// <param name="state">Current state</param>
        /// <returns>Modified state</returns>
        TState Handle(IEvent e, TState state);
    }

    /// <inheritdoc />
    public interface IProjectionHandler<TState, TEvent> : IProjectionHandler<TState>
    {
        /// <summary>
        /// Modify state according to event
        /// </summary>
        /// <param name="e">Event to handle</param>
        /// <param name="state">Current state</param>
        /// <returns>Modified state</returns>
        TState Handle(TEvent e, TState state);
    }
}