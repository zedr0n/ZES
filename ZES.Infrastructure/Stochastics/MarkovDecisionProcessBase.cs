using System;
using System.Collections.Generic;
using System.Linq;
using ZES.Interfaces;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public abstract class MarkovDecisionProcessBase<TState> : IMarkovDecisionProcess<TState>
        where TState : IMarkovState 
    {
        private readonly int _maxIterations;
        private readonly TState _initialState;
        private int _iteration;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkovDecisionProcessBase{TState}"/> class.
        /// </summary>
        /// <param name="initialState">Starting state</param>
        /// <param name="maxIterations">Maximum number of iterations</param>
        protected MarkovDecisionProcessBase(TState initialState, int maxIterations)
        {
            _initialState = initialState;
            _maxIterations = maxIterations;
            Rewards = new List<IActionReward<TState>>();
        }
        
        /// <summary>
        /// Gets or sets the log service
        /// </summary>
        public ILog Log { get; set; }
        
        /// <summary>
        /// Gets or sets reward set for the process
        /// </summary>
        protected List<IActionReward<TState>> Rewards { get; set; }
        
        private Dictionary<IPolicy<TState>, Dictionary<int, IValueFunction<TState>>> ValueFunctions { get; } = new Dictionary<IPolicy<TState>, Dictionary<int, IValueFunction<TState>>>();
        
        /// <summary>
        /// Gets or sets the state space categorized by the distance from the initial state
        /// </summary>
        private Dictionary<int, List<TState>> StateSpace { get; set; }

        /// <inheritdoc />
        public double GetOptimalValue(IPolicy<TState> policy, double tolerance = 0.0001)
        {
            return GetOptimalValue(policy as IDeterministicPolicy<TState>);
        }
        
        /// <summary>
        /// Value function constructor
        /// </summary>
        /// <returns>Associated value function for next iteration</returns>
        protected abstract IValueFunction<TState> NextFunction();

        private double GetOptimalValue(IDeterministicPolicy<TState> policy, double tolerance = 1e-4)
        {
            Initialize(policy);
            var prevValue = 0.0;
            var value = Iterate(policy);
            var change = double.MaxValue;
            while ((change > tolerance || change == 0 || value == 0) && _iteration < _maxIterations && StateSpace[_iteration].Count > 0)
            {
                Log?.Info($"Iteration {_iteration} : {prevValue} -> {value} \t {value - prevValue}");
                prevValue = value;
                value = Iterate(policy);
                change = Math.Abs(value - prevValue);
            }

            return value;
        }
        
        /// <summary>
        /// Populate states reachable from initial state within the current number of iterations
        /// </summary>
        private void ExtendStateSpace(IDeterministicPolicy<TState> policy)
        {
            var previousLayer = StateSpace[_iteration - 1];
            if (StateSpace.ContainsKey(_iteration))
                return;
            var layer = new List<TState>();
            foreach (var state in previousLayer)
            {
                var action = policy[state];
                if (action != null) 
                    layer.AddRange(action[state]);
            }

            StateSpace[_iteration] = layer.Distinct().ToList();
        }

        private void Initialize(IPolicy<TState> policy)
        {
            StateSpace = new Dictionary<int, List<TState>>
            {
                { 0, new List<TState> { _initialState } },
            };
           
            ValueFunctions.Clear();
            ValueFunctions[policy] = new Dictionary<int, IValueFunction<TState>>
            {
                { 0, new ZeroValueFunction<TState>() },
            };
        }

        private void ComputeValueFunction(int iteration, IDeterministicPolicy<TState> policy, IEnumerable<TState> states)
        {
            if (!ValueFunctions[policy].TryGetValue(iteration - 1, out var previousFunction))
                throw new InvalidOperationException("Previous function not available");

            if (!ValueFunctions[policy].ContainsKey(iteration))
                ValueFunctions[policy][iteration] = NextFunction();

            var function = ValueFunctions[policy][iteration];

            foreach (var state in states)
            {
                var value = 0.0;
                var action = policy[state];
                if (action != null)
                {
                    var expectation = 0.0;
                    var expectedReward = 0.0;
                    foreach (var nextState in action[state])
                    {
                        var probability = action[state, nextState];
                        if (probability == 0)
                            continue;
                       
                        expectation += probability * previousFunction[nextState];
                        foreach (var reward in Rewards)
                            expectedReward += probability * reward[state, nextState, action];
                    }

                    value += expectedReward + expectation;
                }
                    
                function[state] = value;
            }   
        }

        private double Iterate(IPolicy<TState> iPolicy)   
        {
            _iteration++;
            var policy = iPolicy as IDeterministicPolicy<TState>;
            if (policy == null)
                throw new InvalidCastException("Policy should be deterministic");
            
            // add newly reachable states
            ExtendStateSpace(policy);
            
            // populate previous needed values
            for (var i = 1; i < _iteration; ++i)
                ComputeValueFunction(i, policy, StateSpace[_iteration - i]);
           
            // compute current iteration result
            ComputeValueFunction(_iteration, policy, new List<TState> { _initialState });

            return ValueFunctions[policy][_iteration][_initialState];
        }
    }
}