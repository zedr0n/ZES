using System.Collections.Generic;
using System.Linq;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public abstract class MarkovActionBase<TState> : IMarkovAction<TState>
        where TState : IMarkovState
    {
        /// <inheritdoc />
        public abstract IEnumerable<TState> this[TState current] { get; }

        /// <inheritdoc />
        public bool this[TState from, TState to] => this[from].Any(s => s.Equals(to));
    }
}