using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;

namespace ZES
{
    /// <summary>
    /// Command state holder
    /// </summary>
    public class CommandStateHolder : StateHolder<CommandStateHolder.State, CommandStateHolder.Builder>
    {
        /// <summary>
        /// Gets the status of a command
        /// </summary>
        /// <returns>Set of failed commands observable</returns>
        public IObservable<CommandState> CommandState()
        {
            return Project(state =>
            {
                if (state.HasFailed)
                    return Interfaces.CommandState.Failed;
                
                return state.Counter > 0 ? Interfaces.CommandState.Executing : Interfaces.CommandState.Complete;
            });
        }

        /// <summary>
        /// Command state
        /// </summary>
        public record struct State(int Counter = 0, bool HasFailed = false) {}

        /// <inheritdoc />
        public struct Builder : IHeldStateBuilder<State, Builder>
        {
            /// <summary>
            /// Completion counter
            /// </summary>
            public int Counter { get; set; }
                
            /// <summary>
            /// True if the command has failed
            /// </summary>
            public bool HasFailed { get; set; }

            /// <inheritdoc />
            public void InitializeFrom(State state)
            {
                Counter = state.Counter;
                HasFailed = state.HasFailed;
            }

            /// <inheritdoc />
            public State Build() => new(Counter, HasFailed);

            /// <inheritdoc />
            public State DefaultState() => new();
        }
    }
}