namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov decision process
    /// </summary>
    /// <typeparam name="TState">State</typeparam>
    /// <typeparam name="TProbability">Transition probability</typeparam>
    public interface IMarkovDecisionProcess<TState, TProbability>
        where TState : IMarkovState
        where TProbability : ITransitionProbability<TState>
    {
        /// <summary>
        /// Get the optimal value for the evaluated policy
        /// </summary>
        /// <param name="policy">used policy</param>
        /// <param name="tolerance">Target tolerance</param>
        /// <returns>Optimal value</returns>
        double GetOptimalValue(IPolicy<TState> policy, double tolerance = 1e-4);
    }
}