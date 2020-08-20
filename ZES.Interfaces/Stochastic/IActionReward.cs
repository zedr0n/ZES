namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Action reward 
    /// </summary>
    /// <typeparam name="TState">State</typeparam>
    public interface IActionReward<TState>
        where TState : IMarkovState
    {
        /// <summary>
        /// Reward indexer
        /// </summary>
        /// <param name="from">Source state</param>
        /// <param name="to">Target state</param>
        /// <param name="action">Applicable action</param>
        double this[TState from, TState to, IMarkovAction<TState> action] { get; }
    }
}