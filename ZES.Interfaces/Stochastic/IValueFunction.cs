namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Optimal value function
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public interface IValueFunction<in TState> 
        where TState : IMarkovState
    {
        /// <summary>
        /// Value corresponding to the state 
        /// </summary>
        /// <param name="s">State</param>
        double this[TState s] { get; set; }
    }
}