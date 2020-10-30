using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZES.Infrastructure.Branching;
using ZES.Infrastructure.Utils;

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
        /// Is retroactive execution active
        /// </summary>
        /// <returns>Retroactive flag</returns>
        public IObservable<bool> RetroactiveExecution()
        {
            return Project(x => x.RetroactiveId != default);
        }

        /// <summary>
        /// Held state
        /// </summary>
        public struct State
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="State"/> struct.
            /// </summary>
            /// <param name="timeline">Timeline</param>
            /// <param name="count">Number of uncomplete messages</param>
            /// <param name="retroactiveId">Retroactive command id being executed</param>
            public State(string timeline, int count, Guid retroactiveId = default)
            {
                Timeline = timeline;
                Count = count;
                RetroactiveId = retroactiveId;
            }

            /// <summary>
            /// Gets the associated timeline
            /// </summary>
            public string Timeline { get; }
            
            /// <summary>
            /// Gets the number of uncompleted messages
            /// </summary>
            public int Count { get; }
            
            /// <summary>
            /// Gets the current retroactive id being executed
            /// </summary>
            public Guid RetroactiveId { get; }
        }

        /// <inheritdoc />
        public struct Builder : IHeldStateBuilder<State, Builder>
        {
            /// <summary>
            /// Gets or sets the number of uncompleted messages on the branch 
            /// </summary>
            public int Count { get; set; }

            /// <summary>
            /// Gets or sets the current retroactive command id being executed
            /// </summary>
            public Guid RetroactiveId { get; set; }
            
            /// <summary>
            /// Gets or sets the associated timeline
            /// </summary>
            public string Timeline { get; set; }

            /// <inheritdoc />
            public void InitializeFrom(State state)
            {
                Count = state.Count;
                Timeline = state.Timeline;
                RetroactiveId = state.RetroactiveId;
            }

            /// <inheritdoc />
            public State Build()
            {
                return new State(Timeline, Count, RetroactiveId); 
            }

            /// <inheritdoc />
            public State DefaultState()
            {
                return new State(BranchManager.Master, 0);
            }
        }
    }
}