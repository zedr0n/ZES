using System.Collections.Generic;

namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov decision process
    /// </summary>
    /// <typeparam name="TState">State</typeparam>
    public interface IMarkovDecisionProcess<TState>
        where TState : IMarkovState
    {
        /// <summary>
        /// Gets or sets reward set for the process
        /// </summary>
        List<IActionReward<TState>> Rewards { get; set; }

        /// <summary>
        /// Get the optimal value for the evaluated policy
        /// </summary>
        /// <param name="policy">used policy</param>
        /// <param name="tolerance">Target tolerance</param>
        /// <returns>Optimal value</returns>
        double GetOptimalValue(IPolicy<TState> policy, double tolerance = 1e-4);
        
        /// <summary>
        /// Get the optimal value for the evaluated policy
        /// </summary>
        /// <param name="policy">used policy</param>
        /// <param name="tolerance">Target tolerance</param>
        /// <returns>Optimal value</returns>
        Value GetOptimalValueAndVariance(IPolicy<TState> policy, double tolerance = 1e-4);
    }
}