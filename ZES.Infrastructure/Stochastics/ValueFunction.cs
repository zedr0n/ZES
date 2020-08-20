using System;
using System.Collections.Generic;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public class ValueFunction<TState> : IValueFunction<TState>
        where TState : IMarkovState, IEquatable<TState>
    {
        private readonly Dictionary<TState, double> _map = new Dictionary<TState, double>();

        /// <inheritdoc />
        public double this[TState s]
        {
            get => _map[s];
            set => _map[s] = value;
        }
    }
}