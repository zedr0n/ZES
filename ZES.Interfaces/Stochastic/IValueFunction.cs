using System;
using System.Collections;

namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Optimal value function
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public interface IValueFunction<TState> 
        where TState : IMarkovState, IEquatable<TState>
    {
        /// <summary>
        /// Value corresponding to the state 
        /// </summary>
        /// <param name="s">State</param>
        double this[TState s] { get; set; }

        bool HasState(TState state);
    }
}