using System.Collections.Generic;

namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov decision process action
    /// </summary>
    /// <typeparam name="TState">Markov state</typeparam>
    public interface IMarkovAction<TState>
        where TState : IMarkovState
    {
        /// <summary>
        /// Possible states indexer
        /// </summary>
        /// <param name="current">Originating state</param>
        IEnumerable<TState> this[TState current] { get; }
        
        /// <summary>
        /// Transition probability of the action
        /// </summary>
        /// <param name="from">Source state</param>
        /// <param name="to">Target state</param>
        double this[TState from, TState to] { get; }
    }
}