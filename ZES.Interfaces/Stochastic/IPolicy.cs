namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov decision process policy
    /// </summary>
    /// <typeparam name="TState">State space type</typeparam>
    public interface IPolicy<TState>
        where TState : IMarkovState
    {
    }
}