using System;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc cref="IMarkovState" />
    public readonly record struct BranchState(string Timeline) : IMarkovState
    {
    }
}