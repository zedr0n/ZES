using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZES.Infrastructure.Branching;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;

namespace ZES
{
    /// <inheritdoc />
    public class UncompletedMessagesSingleHolder : StateHolder<UncompletedMessagesSingleHolder.State, UncompletedMessagesSingleHolder.Builder>
    {
        /// <summary>
        /// Counter of uncompleted messages
        /// </summary>
        /// <returns>Observable representing the counter state</returns>
        public IObservable<int> UncompletedMessages()
        {
            return Project(x => x.Count);
        }

        /// <summary>
        /// Held state
        /// </summary>
        public record struct State(string Timeline, int Count);

        /// <inheritdoc />
        public struct Builder : IHeldStateBuilder<State, Builder>
        {
            /// <summary>
            /// Gets or sets the number of uncompleted messages on the branch 
            /// </summary>
            public int Count { get; set; }

            /// <summary>
            /// Gets or sets the associated timeline
            /// </summary>
            public string Timeline { get; set; }

            /// <inheritdoc />
            public void InitializeFrom(State state)
            {
                Count = state.Count;
                Timeline = state.Timeline;
            }

            /// <inheritdoc />
            public State Build()
            {
                return new State(Timeline, Count); 
            }

            /// <inheritdoc />
            public State DefaultState()
            {
                return new State(BranchManager.Master, 0);
            }
        }
    }
}