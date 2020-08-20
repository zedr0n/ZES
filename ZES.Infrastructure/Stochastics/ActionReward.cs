using System.Diagnostics;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <summary>
    /// Action reward 
    /// </summary>
    /// <typeparam name="TState">State</typeparam>
    /// <typeparam name="TAction">Action type</typeparam>
    [DebuggerStepThrough]
    public abstract class ActionReward<TState, TAction> : IActionReward<TState>
        where TState : IMarkovState
        where TAction : class, IMarkovAction<TState>
    {
        /// <summary>
        /// Reward indexer
        /// </summary>
        /// <param name="from">Source state</param>
        /// <param name="to">Target state</param>
        /// <param name="action">Applicable action</param>
        public abstract double this[TState from, TState to, TAction action] { get; }

        /// <inheritdoc />
        public double this[TState from, TState to, IMarkovAction<TState> action]
        {
            get
            {
                if (action is TAction markovAction && action[from, to])
                    return this[from, to, markovAction];
                return 0.0;
            }
        }
    }
}