using System;

namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Deterministic policy for markov decision processes
    /// </summary>
    /// <typeparam name="TState">State space type</typeparam>
    public interface IDeterministicPolicy<TState> : IPolicy<TState>, ICloneable
        where TState : IMarkovState
    {
        /// <summary>
        /// Gets or sets a value indicating whether gets the value indicating if the policy has been modified
        /// </summary>
        bool IsModified { get; set; }
        
        /// <summary>
        /// Gets the set of modified actions of the policy
        /// </summary>
        TState[] Modifications { get; }
        
        /// <summary>
        /// Decision policy as S -> A map
        /// </summary>
        /// <param name="state">Current state</param>
        IMarkovAction<TState> this[TState state] { get; set; }

        /// <summary>
        /// Enumerates all actions available from state
        /// </summary>
        /// <param name="state">Current state</param>
        /// <returns>Set of possible actions</returns>
        IMarkovAction<TState>[] GetAllowedActions(TState state);
    }
}