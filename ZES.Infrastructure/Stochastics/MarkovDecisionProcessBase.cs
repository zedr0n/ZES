using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces;
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
        /// <param name="rewards">Rewards for all actions</param>
        /// <param name="maxIterations">Maximum number of iterations</param>
        protected MarkovDecisionProcessBase(TState initialState, TProbability transitionProbability, IEnumerable<IMarkovAction<TState>> actions, IEnumerable<IActionReward<TState>> rewards, int maxIterations = 100)
        {
            _initialState = initialState;
            StateSpace = new Dictionary<int, List<TState>>
            {
                { 0, new List<TState> { _initialState } },
            };
            _maxIterations = maxIterations;
            Probability = transitionProbability;
            Actions = actions.ToList();
            Rewards = rewards.ToList();
        }
        
        /// <summary>
        /// Gets or sets the log service
        /// </summary>
        public ILog Log { get; set; }
        
        private TProbability Probability { get; }
        private List<IMarkovAction<TState>> Actions { get; }
        private Dictionary<IPolicy<TState>, Dictionary<int, IValueFunction<TState>>> ValueFunctions { get; } = new Dictionary<IPolicy<TState>, Dictionary<int, IValueFunction<TState>>>();
        private List<IActionReward<TState>> Rewards { get; }
        
        /// <summary>
        /// Gets the state space categorized by the distance from the initial state
        /// </summary>
        private Dictionary<int, List<TState>> StateSpace { get; }

        /// <inheritdoc />
        public double GetOptimalValue(IPolicy<TState> policy, double tolerance = 1e-4)
        {
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
        /// Value function constructor
        /// </summary>
        /// <returns>Associated value function for next iteration</returns>
        protected abstract IValueFunction<TState> NextFunction();
        
        /// <summary>
        /// Populate states reachable from initial state within the current number of iterations
        /// </summary>
        private void ExtendStateSpace(IPolicy<TState> policy)
        {
            var previousLayer = StateSpace[_iteration - 1];
            if (StateSpace.ContainsKey(_iteration))
                return;
            var layer = new List<TState>();
            foreach (var state in previousLayer)
            {
                foreach (var action in Actions)
                {
                    if (policy[action, state] > 0)
                        layer.AddRange(action[state]);
                }
            }

            StateSpace[_iteration] = layer.Distinct().ToList();
        }

        /*private class ActionFlow<TState> : Dataflow<(IMarkovAction<TState> action, TState state ), IEnumerable<TState>>
            where TState : IMarkovState
        {
            private int _parallelCount = 0;
            
            public ActionFlow(DataflowOptions dataflowOptions) 
                : base(dataflowOptions)
            {
                var block = new TransformBlock<(IMarkovAction<TState>, TState),IEnumerable<TState>>(
                    x =>
                    {
                        Interlocked.Increment(ref _parallelCount);
                        var list = new List<TState>();
                        var (action, state) = x;
                        list.AddRange(action[state]);
                        Interlocked.Decrement(ref _parallelCount);
                        return list;
                    }, dataflowOptions.ToExecutionBlockOption(true));
                
                RegisterChild(block);
                InputBlock = block;
                OutputBlock = block;
            }

            public override ITargetBlock<(IMarkovAction<TState> action, TState state)> InputBlock { get; }
            public override ISourceBlock<IEnumerable<TState>> OutputBlock { get; }
        }*/

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
                   var expectedReward = 0.0;
                   foreach (var nextState in action[state])
                   {
                       var probability = Probability[state, nextState, action];
                       if (probability == 0)
                           continue;
                       
                       expectation += probability * previousFunction[nextState];
                       foreach (var reward in Rewards)
                           expectedReward += probability * reward[state, nextState, action];
                   }

                   value += policy[action, state] * (expectedReward + expectation);
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