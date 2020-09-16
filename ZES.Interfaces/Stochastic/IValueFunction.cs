using System;
using System.Collections;

namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Optimal value function
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public interface IValueFunction<in TState> 
        where TState : IMarkovState, IEquatable<TState>
    {
        /// <summary>
        /// Value corresponding to the state 
        /// </summary>
        /// <param name="s">State</param>
        double this[TState s] { get; set; }

        /// <summary>
        /// Check if the state was already computed
        /// </summary>
        /// <param name="state">State</param>
        /// <returns>True if computed</returns>
        bool HasState(TState state);
        
        /// <summary>
        /// Associate the value with state
        /// </summary>
        /// <param name="s">State</param>
        /// <param name="value">Corresponding value</param>
        void Add(TState s, double value);
    }
}