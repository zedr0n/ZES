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
        /// Gets the retroactive status of the command
        /// </summary>
        /// <returns>Observable indicating the retroactive status</returns>
        public IObservable<bool> Retroactive()
        {
            return Project(state => state.IsRetroactive);
        }

        /// <summary>
        /// Gets the list of currently executing retroactive commands
        /// </summary>
        /// <returns></returns>
        public IObservable<List<MessageId> > RetroactiveCommands()
        {
            throw new InvalidOperationException();
        }
        
        /// <summary>
        /// Command state
        /// </summary>
        public record struct State(int Counter = 0, bool IsRetroactive = false, bool HasFailed = false) {}

        /// <inheritdoc />
        public struct Builder : IHeldStateBuilder<State, Builder>
        {
            /// <summary>
            /// Completion counter
            /// </summary>
            public int Counter { get; set; }
                
            /// <summary>
            /// True if the command is retroactive
            /// </summary>
            public bool IsRetroactive { get; set; }
                
            /// <summary>
            /// True if the command has failed
            /// </summary>
            public bool HasFailed { get; set; }

            /// <inheritdoc />
            public void InitializeFrom(State state)
            {
                Counter = state.Counter;
                IsRetroactive = state.IsRetroactive;
                HasFailed = state.HasFailed;
            }

            /// <inheritdoc />
            public State Build() => new(Counter, IsRetroactive, HasFailed);

            /// <inheritdoc />
            public State DefaultState() => new();
        }
    }
}