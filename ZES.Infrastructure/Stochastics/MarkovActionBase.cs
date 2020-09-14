#define USE_ACTION_STATE_CACHE

using System.Collections.Generic;
using System.Linq;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public abstract class MarkovActionBase<TState> : IMarkovAction<TState>
        where TState : IMarkovState
    {
        private Dictionary<TState, TState[]> NextStates { get; } = new Dictionary<TState, TState[]>();
        
        /// <inheritdoc />
        public virtual IEnumerable<TState> this[TState current]
        {
            get
            {
            #if USE_ACTION_STATE_CACHE
                if (!NextStates.ContainsKey(current))
                    NextStates[current] = GetStates(current);

                return NextStates[current];
            #else
                return GetStates(current);
            #endif
            }
        }

        /// <inheritdoc />
        public virtual double this[TState from, TState to] => this[from].Contains(to) ? 1.0 : 0.0;
        
        /// <summary>
        /// Generate the possible states using current action
        /// </summary>
        /// <param name="current">Current state</param>
        /// <returns>Possible states</returns>
        protected abstract TState[] GetStates(TState current);
    }
}