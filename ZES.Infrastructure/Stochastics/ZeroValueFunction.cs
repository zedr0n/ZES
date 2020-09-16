using System;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <summary>
    /// Initial value function ( always 0 )
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public class ZeroValueFunction<TState> : IValueFunction<TState>
        where TState : IMarkovState, IEquatable<TState>
    {
        /// <summary>
        /// Function indexer
        /// </summary>
        /// <param name="s">State</param>
        public double this[TState s]
        {
            get => 0.0;
            set { } 
        }

        /// <inheritdoc />
        public bool HasState(TState state) => false;

        /// <inheritdoc />
        public void Add(TState s, double value) { }
    }
}