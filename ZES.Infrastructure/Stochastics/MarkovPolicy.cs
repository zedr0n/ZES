using System.Collections.Generic;
using System.Linq;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public abstract class MarkovPolicy<TState> : IDeterministicPolicy<TState>
        where TState : IMarkovState
    {
        private readonly HashSet<TState> _optimals = new HashSet<TState>();
        private Dictionary<TState, IMarkovAction<TState>> _modifications = new Dictionary<TState, IMarkovAction<TState>>();
        private Dictionary<TState, IMarkovAction<TState>[]> _actions = new Dictionary<TState, IMarkovAction<TState>[]>();

        /// <inheritdoc />
        public bool IsModified { get; set; }

        /// <inheritdoc />
        public TState[] Modifications => _modifications.Keys.ToArray();

        /// <inheritdoc />
        public IMarkovAction<TState> this[TState state]
        {
            get
            {
                if (_modifications.TryGetValue(state, out var action))
                    return action;

                return GetAction(state);
            }
            set
            {
                _optimals.Add(state);
                if (value == null)
                    return;
                
                if (!_modifications.TryGetValue(state, out var current))
                    current = this[state];

                if (current == value)
                    return;
                
                IsModified = true;
                _modifications[state] = value;
            } 
        }

        /// <inheritdoc />
        public object Clone()
        {
            var clone = Copy();
            clone._actions = _actions;
            clone._modifications = new Dictionary<TState, IMarkovAction<TState>>(_modifications);
            return clone;
        }

        /// <inheritdoc />
        public bool HasOptimal(TState state)
        {
            if (_optimals.Count == 0)
                return true;

            return _optimals.Contains(state);
        }
        
        /// <inheritdoc />
        public IMarkovAction<TState>[] GetAllowedActions(TState state)
        {
            if (_actions.TryGetValue(state, out var actions)) 
                return actions;
            
            actions = GetAllActions(state);
            _actions.Add(state, actions);
            return actions;
        }

        /// <summary>
        /// Virtual constructor
        /// </summary>
        /// <returns>Policy instance</returns>
        protected abstract MarkovPolicy<TState> Copy();

        /// <summary>
        /// Get all possible actions from the state 
        /// </summary>
        /// <param name="state">Current state</param>
        /// <returns>Set of all possible actions</returns>
        protected abstract IMarkovAction<TState>[] GetAllActions(TState state);

        /// <summary>
        /// Calculate the action implied by the policy for the specified state
        /// </summary>
        /// <param name="state">Current state</param>
        /// <returns>Policy action</returns>
        protected abstract IMarkovAction<TState> GetAction(TState state);
    }
}