using System.Collections.Generic;
using System.Linq;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <summary>
    /// Policy for value iteration
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public class OptimalPolicy<TState> : IDeterministicPolicy<TState>
        where TState : IMarkovState
    {
        private readonly IDeterministicPolicy<TState> _basePolicy;
        private readonly Dictionary<TState, IMarkovAction<TState>> _actions;
        private readonly Dictionary<TState, IMarkovAction<TState>[]> _allActions;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimalPolicy{TState}"/> class.
        /// </summary>
        /// <param name="basePolicy">Base policy to modify</param>
        public OptimalPolicy(IDeterministicPolicy<TState> basePolicy)
        {
            _basePolicy = basePolicy;
            _actions = new Dictionary<TState, IMarkovAction<TState>>();
            _allActions = new Dictionary<TState, IMarkovAction<TState>[]>();
        }

        /// <inheritdoc />
        public bool IsModified { get; set; } = true;

        /// <inheritdoc />
        public TState[] Modifications => _actions.Keys.ToArray();

        /// <inheritdoc />
        public IMarkovAction<TState> this[TState state]
        {
            get
            {
                if (_actions.TryGetValue(state, out var action))
                    return action;
                return _basePolicy[state];
            }
            set => _actions[state] = value;
        }

        /// <summary>
        /// Enumerate next actions from state
        /// </summary>
        /// <param name="state">Current state</param>
        /// <returns>Next actions from policy</returns>
        public IEnumerable<IMarkovAction<TState>> GetNextActions(TState state)
        {
            var action = this[state];
            var nextStates = action[state];
            var set = new HashSet<IMarkovAction<TState>>();
            foreach (var nextState in nextStates)
            {
                var nextAction = this[nextState];
                if (nextAction != null)
                    set.Add(nextAction);
            }

            return set;
        }

        /// <summary>
        /// Get possible actions from policy 
        /// </summary>
        /// <param name="state">Current state</param>
        /// <returns>Set of possible actions</returns>
        public IMarkovAction<TState>[] GetAllowedActions(TState state)
        {
            if (_allActions.TryGetValue(state, out var actions))
                return actions;
            actions = _basePolicy.GetAllowedActions(state);
            _allActions[state] = actions;
            return actions;
        }

        /// <inheritdoc />
        public object Clone()
        {
            throw new System.NotImplementedException();
        }
    }
}