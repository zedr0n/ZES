namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Builder for async state
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    /// <typeparam name="TBuilder">State builder</typeparam>
    public interface IHeldStateBuilder<TState, TBuilder>
    {
        /// <summary>
        /// Initialize the state from given
        /// </summary>
        /// <param name="state">State value</param>
        void InitializeFrom(TState state);
        
        /// <summary>
        /// Build the state
        /// </summary>
        /// <returns>Built state</returns>
        TState Build();
        
        /// <summary>
        /// Gets the default state
        /// </summary>
        /// <returns>Default state</returns>
        TState DefaultState();
    }
}