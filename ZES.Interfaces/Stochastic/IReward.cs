namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov decision process reward function
    /// </summary>
    /// <typeparam name="TState">Markov state</typeparam>
    /// <typeparam name="TAction">Markov action</typeparam>
    public interface IReward<in TState, in TAction>
        where TState : IMarkovState
        where TAction : IMarkovAction<TState>
    {
        /// <summary>
        /// Reward indexer
        /// </summary>
        /// <param name="state">Source state</param>
        /// <param name="action">Associated action</param>
        double this[TState state, TAction action] { get; }    
    }

    /// <summary>
    /// Markov decision process reward function
    /// </summary>
    /// <typeparam name="TState">Markov state</typeparam>
    public interface IReward<TState>
        where TState : IMarkovState
    {
        /// <summary>
        /// Reward indexer
        /// </summary>
        /// <param name="action">Associated action</param>
        /// <param name="state">Source state</param>
        double this[IMarkovAction<TState> action, TState state] { get; }    
    }
}