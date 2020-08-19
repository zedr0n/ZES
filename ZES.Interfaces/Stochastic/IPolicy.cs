namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov decision process policy : P(a|s)
    /// </summary>
    /// <typeparam name="TState">Markov state</typeparam>
    public interface IPolicy<TState>
        where TState : IMarkovState
    {
        /// <summary>
        /// Policy indexer
        /// </summary>
        /// <param name="a">Associated action</param>
        /// <param name="s">Current state</param>
        double this[IMarkovAction<TState> a, TState s] { get; }
    }
}