using System;
using System.Collections.Generic;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public class ValueFunction<TState> : IValueFunction<TState>
        where TState : IMarkovState, IEquatable<TState>
    {
        private readonly Dictionary<TState, Value> _map = new Dictionary<TState, Value>();

        /// <inheritdoc />
        public Value this[TState s]
        {
            get => _map[s];
            set => _map[s] = value;
        }

        /// <inheritdoc />
        public void Add(TState s, Value value) => _map.Add(s, value);
        
        /// <inheritdoc />
        public bool HasState(TState state) => _map.ContainsKey(state);
    }
}