using System.Collections.Generic;

namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov decision process policy : P(a|s)
    /// </summary>
    /// <typeparam name="TState">Markov state</typeparam>
    public interface IGeneralizedPolicy<TState> : IPolicy<TState>
        where TState : IMarkovState
    {
        /// <summary>
        /// Policy indexer
        /// </summary>
        /// <param name="a">Associated action</param>
        /// <param name="s">Current state</param>
        double this[IMarkovAction<TState> a, TState s] { get; }
        
        /// <summary>
        /// Gets the set of actions which have non-zero probability
        /// </summary>
        /// <returns>Set of possible actions</returns>
        IEnumerable<IMarkovAction<TState>> GetAllowedActions();
    }
}