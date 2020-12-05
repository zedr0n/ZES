using System;
using System.Collections.Generic;
using System.Linq;
using ZES.Infrastructure.Branching;
using ZES.Infrastructure.Utils;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class FailedCommandsSingleHolder : StateHolder<FailedCommandsSingleHolder.State, FailedCommandsSingleHolder.Builder>
    {
        /// <summary>
        /// Gets the sets of failed commands
        /// </summary>
        /// <returns>Set of failed commands observable</returns>
        public IObservable<HashSet<ICommand>> FailedCommands()
        {
            return Project(x => x.Commands);
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
            /// <param name="commands">Set of failed commands</param>
            public State(string timeline, HashSet<ICommand> commands)
            {
                Timeline = timeline;
                Commands = commands;
            }

            /// <summary>
            /// Gets the associated timeline
            /// </summary>
            public string Timeline { get; }
            
            /// <summary>
            /// Gets the set of failed commands
            /// </summary>
            public HashSet<ICommand> Commands { get; }
        }

        /// <inheritdoc />
        public struct Builder : IHeldStateBuilder<State, Builder>
        {
            /// <summary>
            /// Gets or sets the set of failed commands
            /// </summary>
            public HashSet<ICommand> Commands { get; set; }
            
            /// <summary>
            /// Gets or sets the associated timeline
            /// </summary>
            public string Timeline { get; set; }

            /// <inheritdoc />
            public void InitializeFrom(State state)
            {
                Timeline = state.Timeline;
                Commands = new HashSet<ICommand>(state.Commands);
            }

            /// <inheritdoc />
            public State Build()
            {
                return new State(Timeline, Commands); 
            }

            /// <inheritdoc />
            public State DefaultState()
            {
                return new State(BranchManager.Master, new HashSet<ICommand>());
            }
        }
    }
}