using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZES.Infrastructure.Utils;

namespace ZES
{
    /// <inheritdoc />
    public class UncompletedMessagesHolder : StateHolder<UncompletedMessagesHolder.State, UncompletedMessagesHolder.Builder>
    {
        /// <summary>
        /// Counter of uncompleted messages
        /// </summary>
        /// <param name="timeline">Timeline id</param>
        /// <returns>Observable representing the counter state</returns>
        public IObservable<int> UncompletedMessages(string timeline)
        {
            return Project(x =>
            {
                if (x.Count.TryGetValue(timeline, out var count))
                    return count;
                return 0;
            });
        }

        /// <summary>
        /// Held state
        /// </summary>
        public struct State
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="State"/> struct.
            /// </summary>
            /// <param name="count">Count dictionary</param>
            public State(Dictionary<string, int> count)
            {
                Count = count; // new ConcurrentDictionary<string, int>(count); 
            }
            
            /// <summary>
            /// Gets current count per branch 
            /// </summary>
            public Dictionary<string, int> Count { get; }
        }

        /// <inheritdoc />
        public struct Builder : IHeldStateBuilder<State, Builder>
        {
            /// <summary>
            /// Gets or sets [branch, number of uncompleted messages]
            /// </summary>
            public Dictionary<string, int> Count { get; set; }

            /// <inheritdoc />
            public void InitializeFrom(State state)
            {
                Count = new Dictionary<string, int>(state.Count);
            }

            /// <inheritdoc />
            public State Build()
            {
                return new State(Count); 
            }

            /// <inheritdoc />
            public State DefaultState()
            {
                return new State(new Dictionary<string, int>());
            }
        }
    }
}