using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public abstract class MarkovDecisionProcessBase<TState> : IMarkovDecisionProcess<TState>
        where TState : IMarkovState, IEquatable<TState>
    {
        private readonly int _maxIterations;
        private readonly TState _initialState;
        private int _iteration;
        private double _tolerance;

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
            new Dictionary<TState, HashSet<TState>>();
        }
        
        /// <summary>
        /// Gets or sets the log service
        /// </summary>
        public ILog Log { get; set; }

        /// <summary>
        /// Gets or sets reward set for the process
        /// </summary>
        public List<IActionReward<TState>> Rewards { get; set; }

        private LinkedList<IDeterministicPolicy<TState>> Policies { get; } = new LinkedList<IDeterministicPolicy<TState>>();
        private Dictionary<IPolicy<TState>, Dictionary<int, IValueFunction<TState>>> ValueFunctions { get; } = new Dictionary<IPolicy<TState>, Dictionary<int, IValueFunction<TState>>>();
        private Dictionary<TState, double> ReachableStates { get; set; }
        
        /// <summary>
        /// Gets or sets the state space categorized by the distance from the initial state
        /// </summary>
        private Dictionary<int, HashSet<TState>> StateSpace { get; set; }

        private Dictionary<int, List<TState>> DistinctStateSpace { get; set; }
        private HashSet<TState> AllStateSpace { get; set; }

        /// <inheritdoc />
        public double GetOptimalValue(IPolicy<TState> policy, double tolerance = 0.0001, TState[] inputStates = null)
        {
            return GetOptimalValue(policy as IDeterministicPolicy<TState>, tolerance, false, inputStates).Mean;
        }

        /// <inheritdoc />
        public Value GetOptimalValueAndVariance(IPolicy<TState> policy, double tolerance = 0.0001)
        {
            var val = GetOptimalValue(policy as IDeterministicPolicy<TState>, tolerance);
            var variance = Math.Sqrt(val.Variance - (val.Mean * val.Mean));
            var ret = new Value(val.Mean, variance);
            return ret;
        }

        /// <summary>
        /// Policy iteration for MDP
        /// </summary>
        /// <param name="basePolicy">Starting policy</param>
        /// <param name="optimalPolicy">Resulting optimal policy</param>
        /// <param name="tolerance">Convergence tolerance</param>
        /// <param name="outputStates">States to evaluate</param>
        /// <returns>Optimal value</returns>
        public double GetOptimalValueViaPolicyIteration(
            IDeterministicPolicy<TState> basePolicy, 
            out IDeterministicPolicy<TState> optimalPolicy, 
            double tolerance = 0.0001,
            TState[] outputStates = null)
        {
             ReachableStates = GetReachableStateSpace(basePolicy, _initialState);
             _tolerance = tolerance;
             var node = Policies.AddFirst(basePolicy);
             var policy = basePolicy;
             var value = GetOptimalValue(policy, tolerance, false, outputStates);
             optimalPolicy = policy = PolicyIteration(policy);
             while (policy.IsModified)
             {
                 node = Policies.AddAfter(node, policy);
                 var nextValue = GetOptimalValue(policy, tolerance, false, outputStates);
                 Log?.Info($"{value.Mean}->{nextValue.Mean} with {policy.Modifications.Length} modifications");
                 if (nextValue.Mean < value.Mean)
                     break;
                
                 // if (Math.Abs(nextValue.Mean - value.Mean) < tolerance * 1)
                 //    break;
                
                 value = nextValue;
                 policy = PolicyIteration(policy);
                 if (node.Previous != null)
                     Policies.Remove(node.Previous);
             }

             GetOptimalValue(policy, tolerance, true, outputStates);
             optimalPolicy = (IDeterministicPolicy<TState>)basePolicy.Clone();
             foreach (var state in AllStateSpace)
                 optimalPolicy[state] = policy[state];
             
             return value.Mean;
        }

        /// <summary>
        /// Value function constructor
        /// </summary>
        /// <returns>Associated value function for next iteration</returns>
        protected abstract IValueFunction<TState> NextFunction();

        private static double Norm(IReadOnlyCollection<Value> values)
        {
            var norm = values.Sum(v => v.Mean * v.Mean);
            return Math.Sqrt(norm) / values.Count;
        }

        private List<HashSet<TState>> GetValueDependencies(IDeterministicPolicy<TState> policy, TState state, int depth, IMarkovAction<TState> nextAction = null)
        {
            var deps = new List<HashSet<TState>>(depth + 1);
            var it = 0;
            var states = new HashSet<TState>();
            var valueFunction = ValueFunctions[policy][depth];
            
            // replace the policy action with next action
            if (nextAction != null)
            {
                var actionStates = new List<TState>();
                lock (nextAction)
                    actionStates.AddRange(nextAction[state].Where(s => !valueFunction.HasState(s)));
                states.UnionWith(actionStates);
                depth--;
            }
            else
            {
                states.Add(state);
            }

            deps.Add(states);

            while (depth-- > 0)
            {
                ++it;
                var nextStates = new HashSet<TState>();
                foreach (var s in states)
                {
                    var action = policy[s];
                    if (action == null)
                        continue;
                    IEnumerable<TState> actionStates;
                    lock (action)
                        actionStates = action[s];

                    nextStates.UnionWith(actionStates);
                }

                deps.Add(nextStates);
                states = nextStates;
            }

            return deps;
        }
        
        private Dictionary<TState, double> GetReachableStateSpace(IDeterministicPolicy<TState> policy, TState initialState)
        {
            var dict = new Dictionary<TState, double>();
            var tol = 1e-2;
            var maxProba = 1.0;
            var allStates = new HashSet<TState>();
            var states = new HashSet<TState> { initialState };

            dict[initialState] = 1.0;
            
            while (maxProba > tol && states.Count > 0)
            {
                maxProba = 0.0;
                var allowedStates = new HashSet<TState>();
                foreach (var state in states)
                {
                    var allowedActions = policy.GetAllowedActions(state).Where(a => a != null).ToArray();
                    var stateProba = dict[state];
                    if (stateProba > maxProba)
                        maxProba = stateProba;
                    if (stateProba < tol)
                        continue;
                    
                    foreach (var action in allowedActions)
                    {
                        var nextStates = action[state].ToImmutableHashSet();
                        foreach (var nextState in nextStates)
                        {
                            var p = action[state, nextState];
                           
                            if (dict.TryGetValue(nextState, out var current))
                                dict[nextState] = Math.Max(current, (stateProba * p));
                            else
                                dict.Add(nextState, stateProba * p);
                        }
                        
                        allowedStates.UnionWith(nextStates);
                    }
                }
                
                states.Clear();
                foreach (var allowedState in allowedStates)
                {
                    if (allStates.Add(allowedState))
                        states.Add(allowedState);

                }
            }

            return dict;
        }

        private Dictionary<IMarkovAction<TState>, List<HashSet<TState>>> GetAllowedActionsDependencies( IDeterministicPolicy<TState> policy, TState state)
        {
            var iterations = ValueFunctions[policy].Count - 1;
            var dict = new Dictionary<IMarkovAction<TState>, List<HashSet<TState>>>();
            Parallel.ForEach(policy.GetAllowedActions(state).Where(a => a != null), new ParallelOptions { MaxDegreeOfParallelism = Configuration.ThreadsPerInstance }, action =>
            {
                var deps = GetValueDependencies(policy, state, iterations, action);
                
                lock (dict)
                    dict[action] = deps;
            });
            return dict;
        }

        private IMarkovAction<TState> GetOptimalAction(IDeterministicPolicy<TState> policy, TState state)
        {
            var iterations = ValueFunctions[policy].Count - 1;
            var lastValueFunction = ValueFunctions[policy][iterations];
            var argmax = policy[state];
            var value = new Value(double.MinValue, 0.0);
            if (lastValueFunction.HasState(state))
                value = lastValueFunction[state];

            var dict = GetAllowedActionsDependencies(policy, state);
            foreach (var action in policy.GetAllowedActions(state))
            {
                if (action == null)
                    continue;
                
                var deps = dict[action];
                for (var i = 1; i <= iterations; ++i)
                {
                    var valueFunction = ValueFunctions[policy][i];
                    var states = deps[iterations - i];
                    if (states.Count == 0)
                        continue;
                    var newStates = states.Where(s => !valueFunction.HasState(s));  
                    ComputeValueFunction(i, policy, newStates );
                }

                lastValueFunction = ValueFunctions[policy][iterations];
                var expectation = GetExpectation(action, state, lastValueFunction);
                if (expectation.Mean <= value.Mean) 
                    continue;
                value = new Value(expectation.Mean, expectation.Variance);
                argmax = action;
            }

            return argmax;
        }

        private Value GetExpectation(IMarkovAction<TState> action, TState state, IValueFunction<TState> previousFunction)
        {
            var expectation = new Value(0.0, 0.0);
            foreach (var nextState in action[state])
            {
                var probability = action[state, nextState];
                if (probability == 0)
                    continue;

                var totalReward = 0.0;
                foreach (var reward in Rewards)
                    totalReward += reward[state, nextState, action];

                var prevValue = previousFunction[nextState];

                var rewardValue = new Value(totalReward + prevValue.Mean, prevValue.Variance + (totalReward * totalReward) + (2 * prevValue.Mean * totalReward));
                expectation += probability * rewardValue;
            }

            return expectation;
        }

        private IDeterministicPolicy<TState> PolicyIteration(IDeterministicPolicy<TState> policy)
        {
            var nextPolicy = (IDeterministicPolicy<TState>)policy.Clone();
            
            var progress = 0.0;
            var step = 1.0 / ReachableStates.Count;
            var logStep = 0.01;
            
            Log?.Info("Calculating optimal actions...");
            foreach (var state in ReachableStates.Keys)
            {
                var optimalAction = GetOptimalAction(policy, state);
                if (optimalAction != null)
                    nextPolicy[state] = optimalAction;
                if ( (progress / logStep) - Math.Truncate(progress / logStep) < step / logStep )
                    Log?.Info($"{Math.Truncate(progress * 10000)/100}%");
                progress += step;
            }

            return nextPolicy;
        }

        private Value GetOptimalValue(IDeterministicPolicy<TState> policy, double tolerance = 1e-4, bool ignoreZeroChange = false, TState[] outputStates = null)
        {
            _tolerance = tolerance;
            if (outputStates == null)
                outputStates = new[] { _initialState };
            Initialize(policy, outputStates);
            var values = new Value[outputStates.Length];
            var diff = new Value[outputStates.Length];
            var change = double.MaxValue; 
            while ((change > tolerance || ( change == 0 && !ignoreZeroChange) || values[0].Mean == 0) && _iteration < _maxIterations && StateSpace[_iteration].Count > 0)
            {
                var nextValues = Iterate(policy);

                for (var i = 0; i < outputStates.Length; ++i)
                    diff[i] = nextValues[i] + ((-1) * values[i]);
                change = Norm(diff);
                
                values = nextValues;
                
                // Log?.Info($"Iteration {_iteration} : {prevValue.Mean} -> {value.Mean} \t {value.Mean - prevValue.Mean}");
            }

            return values[0];
        }
        
        /// <summary>
        /// Populate states reachable from initial state within the current number of iterations
        /// </summary>
        private void ExtendStateSpace(IDeterministicPolicy<TState> policy)
        {
            var previousLayer = StateSpace[_iteration - 1];
            if (StateSpace.ContainsKey(_iteration))
                return;
            var isOptimalPolicy = policy is OptimalPolicy<TState>;
            var layer = new HashSet<TState>();
            var distinctLayer = new List<TState>();
            var actions = new IMarkovAction<TState>[1];
            var nextStates = new HashSet<TState>();

            foreach (var state in previousLayer)
            {
                if (isOptimalPolicy)
                    actions = policy.GetAllowedActions(state);
                else 
                    actions[0] = policy[state];

                foreach (var action in actions)
                {
                    if (action == null)
                        continue;
                    
                    nextStates.UnionWith(action[state]);
                }
            }
            
            foreach (var nextState in nextStates)
            {
                if (AllStateSpace.Add(nextState))
                    distinctLayer.Add(nextState);
                        
                layer.Add(nextState); 
            }

            StateSpace[_iteration] = layer;
            DistinctStateSpace[_iteration] = distinctLayer; 
        }

        private void Initialize(IPolicy<TState> policy, TState[] states = null )
        {
            if (states == null)
                states = new[] { _initialState };
            
            StateSpace = new Dictionary<int, HashSet<TState>>
            {
                { 0, new HashSet<TState>(states) }, 
            };

            DistinctStateSpace = new Dictionary<int, List<TState>>
            {
                { 0, new List<TState>(states) },
            };
            
            AllStateSpace = new HashSet<TState>(states); 
           
            // ValueFunctions.Clear();
            ValueFunctions[policy] = new Dictionary<int, IValueFunction<TState>>
            {
                { 0, new ZeroValueFunction<TState>() },
            };
            _iteration = 0;
        }

        private void ComputeValueFunctionParallel(int iteration, IDeterministicPolicy<TState> policy, IEnumerable<TState> states)
        {
            if (!ValueFunctions[policy].TryGetValue(iteration - 1, out var previousFunction))
                throw new InvalidOperationException("Previous function not available");

            if (!ValueFunctions[policy].ContainsKey(iteration))
                ValueFunctions[policy][iteration] = NextFunction();

            var function = ValueFunctions[policy][iteration];
            var isOptimalPolicy = policy is OptimalPolicy<TState>;

            Parallel.ForEach(
                states,
                new ParallelOptions { MaxDegreeOfParallelism = Configuration.ThreadsPerInstance },
                state =>
            {
                var actions = new IMarkovAction<TState>[1];
                if (isOptimalPolicy)
                    actions = policy.GetAllowedActions(state);
                else
                    actions[0] = policy[state];
                
                var optimalValue = new Value(actions.Any(a => a != null) ? double.MinValue : 0.0, 0.0);
                IMarkovAction<TState> optimalAction = null;
               
                foreach (var action in actions)
                {
                    if (action == null) 
                        continue;

                    var expectation = GetExpectation(action, state, previousFunction);
                    lock (function)
                    {
                        if (expectation.Mean >= optimalValue.Mean)
                        {
                            optimalValue = new Value(expectation.Mean, expectation.Variance);
                            optimalAction = action;
                        }
                    }
                }

                lock (function)
                {
                    function.Add(state, optimalValue);
                    if (isOptimalPolicy && optimalAction != null)
                        policy[state] = optimalAction;
                } 
            });
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
                var action = policy[state];
                var expectation = new Value(0.0, 0.0);
                if (action != null)
                    expectation = GetExpectation(action, state, previousFunction); 

                function.Add(state, expectation);
            }
        }

        private Value[] Iterate(IPolicy<TState> iPolicy)   
        {
            _iteration++;
            var policy = iPolicy as IDeterministicPolicy<TState>;
            if (policy == null)
                throw new InvalidCastException("Policy should be deterministic");
            
            // add newly reachable states
            ExtendStateSpace(policy);
            
            // populate previous needed values
            for (var i = 1; i < _iteration; ++i)
                ComputeValueFunction(i, policy, DistinctStateSpace[_iteration - i]);
           
            // compute current iteration result
            ComputeValueFunction(_iteration, policy, StateSpace[0]);

            var valueFunction = ValueFunctions[policy][_iteration];
            var results = StateSpace[0].Select(state => valueFunction[state]).ToArray();
            return results;
        }
    }
}