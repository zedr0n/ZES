using System;
using System.Collections.Generic;
using System.Linq;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public abstract class MarkovDecisionProcessBase<TState, TProbability> : IMarkovDecisionProcess<TState, TProbability>
        where TState : IMarkovState 
        where TProbability : ITransitionProbability<TState>
    {
        private readonly int _maxIterations;
        private readonly TState _initialState;
        private int _iteration;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkovDecisionProcessBase{TState, TProbability}"/> class.
        /// </summary>
        /// <param name="initialState">Starting state</param>
        /// <param name="transitionProbability">Transition probability</param>
        /// <param name="actions">Set of actions</param>
        /// <param name="reward">Reward function</param>
        /// <param name="maxIterations">Maximum number of iterations</param>
        protected MarkovDecisionProcessBase(TState initialState, TProbability transitionProbability, IEnumerable<IMarkovAction<TState>> actions, IReward<TState> reward, int maxIterations = 100)
        {
            _initialState = initialState;
            StateSpace = new Dictionary<int, List<TState>>
            {
                { 0, new List<TState> { _initialState } },
            };
            ValueFunctions = new Dictionary<IPolicy<TState>, Dictionary<int, IValueFunction<TState>>>();
            _maxIterations = maxIterations;
            Probability = transitionProbability;
            Actions = actions.ToList();
            Reward = reward;
        }
        
        private TProbability Probability { get; }
        private List<IMarkovAction<TState>> Actions { get; }
        private IReward<TState> Reward { get; }
        private Dictionary<IPolicy<TState>, Dictionary<int, IValueFunction<TState>>> ValueFunctions { get; }

        /// <summary>
        /// Gets the state space categorized by the distance from the initial state
        /// </summary>
        private Dictionary<int, List<TState>> StateSpace { get; }

        /// <inheritdoc />
        public double GetOptimalValue(IPolicy<TState> policy, double tolerance = 1e-4)
        {
            var prevValue = 0.0;
            var value = Iterate(policy);
            while (Math.Abs(value - prevValue) > tolerance || value == 0)
            {
                prevValue = value;
                value = Iterate(policy);
            }

            return value;
        }

        /// <summary>
        /// Value function constructor
        /// </summary>
        /// <returns>Associated value function for next iteration</returns>
        protected abstract IValueFunction<TState> NextFunction();
        
        /// <summary>
        /// Populate states reachable from initial state within the current number of iterations
        /// </summary>
        private void ExtendStateSpace()
        {
            var previousLayer = StateSpace[_iteration - 1];
            if (StateSpace.ContainsKey(_iteration))
                return;
            var layer = new List<TState>();
            foreach (var s in previousLayer)
            {
                foreach (var a in Actions)
                {
                    layer.AddRange(a[s]);
                }
            }

            StateSpace[_iteration] = layer.Distinct().ToList();
        }

        private void ComputeValueFunction(int iteration, IPolicy<TState> policy, IEnumerable<TState> states)
        {
            if (!ValueFunctions[policy].TryGetValue(iteration - 1, out var previousFunction))
                throw new InvalidOperationException("Previous function not available");

            if (!ValueFunctions[policy].ContainsKey(iteration))
                ValueFunctions[policy][iteration] = NextFunction();

            var function = ValueFunctions[policy][iteration];

            foreach (var state in states)
            {
                var value = 0.0;
                foreach (var action in Actions)
                {
                   if (policy[action, state] == 0)
                       continue;

                   var expectation = 0.0;
                   foreach (var nextState in action[state])
                       expectation += Probability[state, nextState, action] * previousFunction[nextState];
                   value += policy[action, state] * (Reward[action, state] + expectation);
                }

                function[state] = value;
            }   
        }

        private double Iterate(IPolicy<TState> policy)   
        {
            if (!ValueFunctions.ContainsKey(policy))
            {
                ValueFunctions[policy] = new Dictionary<int, IValueFunction<TState>>
                {
                    { 0, new ZeroValueFunction<TState>() },
                };
            }
            
            _iteration++;
            
            // add newly reachable states
            ExtendStateSpace();
            
            // populate previous needed values
            for (var i = 1; i < _iteration; ++i)
                ComputeValueFunction(i, policy, StateSpace[_iteration - i]);
           
            // compute current iteration result
            ComputeValueFunction(_iteration, policy, new List<TState> { _initialState });

            return ValueFunctions[policy][_iteration][_initialState];
        }
    }
}