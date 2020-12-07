using System;
using ZES.Interfaces.Stochastic;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc cref="IMarkovState" />
    public readonly struct BranchState : IMarkovState, IEquatable<BranchState>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BranchState"/> struct.
        /// </summary>
        /// <param name="timeline">Associated timeline</param>
        public BranchState(string timeline)
        {
            Timeline = timeline;
        }

        /// <summary>
        /// Gets the associated timeline for the the state
        /// </summary>
        public string Timeline { get; } 

        /// <inheritdoc />
        public bool Equals(BranchState other)
        {
            return Timeline == other.Timeline;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)2166136261;
                hashCode ^= Timeline.GetHashCode();
                return hashCode;
            }
        }
    }
}