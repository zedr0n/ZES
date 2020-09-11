namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Deterministic policy for markov decision processes
    /// </summary>
    /// <typeparam name="TState">State space type</typeparam>
    public interface IDeterministicPolicy<TState> : IPolicy<TState>
        where TState : IMarkovState
    {
        /// <summary>
        /// Decision policy as S -> A map
        /// </summary>
        /// <param name="state">Current state</param>
        IMarkovAction<TState> this[TState state] { get; set; }
    }
}