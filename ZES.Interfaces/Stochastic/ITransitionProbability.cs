namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov decision process transition probability for the specific action 
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    /// <typeparam name="TAction">Action type</typeparam>
    public interface ITransitionProbability<in TState, in TAction>
        where TState : IMarkovState
    {
        /// <summary>
        /// Transition indexer
        /// </summary>
        /// <param name="from">Source state</param>
        /// <param name="to">Target state</param>
        /// <param name="action">Taken action</param>
        double this[TState from, TState to, TAction action] { get; }
    }

    /// <summary>
    /// Markov decision process transition probability
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public interface ITransitionProbability<TState> 
        where TState : IMarkovState
    {
       /// <summary>
       /// Transition indexer
       /// </summary>
       /// <param name="from">Source state</param>
       /// <param name="to">Target state</param>
       /// <param name="action">Taken action</param>
       double this[TState from, TState to, IMarkovAction<TState> action] { get; } 
    }
}