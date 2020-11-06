using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Stochastics
{
    /// <summary>
    /// Markov action comprised of a set of possible commands
    /// </summary>
    public abstract class BranchAction : MarkovActionBase<BranchState>
    {
        private readonly IBus _bus;
        private readonly IBranchManager _manager;
        private readonly Guid _id;

        /// <summary>
        /// Initializes a new instance of the <see cref="BranchAction"/> class.
        /// </summary>
        /// <param name="manager">Branch manager</param>
        /// <param name="bus">Command bus</param>
        public BranchAction(IBranchManager manager, IBus bus)
        {
            _manager = manager;
            _bus = bus;
            _id = Guid.NewGuid();
        }
        
        /// <summary>
        /// Gets the action identifier
        /// </summary>
        protected string Id => _id.ToString();

        /// <summary>
        /// Gets the commands corresponding to the action
        /// </summary>
        /// <param name="current">Current state</param>
        /// <returns>Possible commands</returns>
        protected abstract IEnumerable<IEnumerable<ICommand>> GetCommands(BranchState current);
        
        /// <inheritdoc />
        protected override BranchState[] GetStates(BranchState current)
        {
            var activeBranch = _manager.ActiveBranch;
            var commands = GetCommands(current).ToList();
            var states = new BranchState[commands.Count];
            var iState = 0;
            foreach (var command in commands)
            {
                states[iState] = ApplyCommands(command, current.Timeline).Result;
                iState++;
            }

            _manager.Branch(activeBranch).Wait();

            return states;
        }

        private async Task<BranchState> ApplyCommands(IEnumerable<ICommand> commands, string timeline)
        {
            await _manager.Branch(timeline);
            var branch = $"{_id.ToString()}";
            await _manager.Branch(branch);

            foreach (var command in commands)
                await _bus.CommandAsync(command);

            await _manager.Branch(timeline);
            return new BranchState(branch);
        }
    }
}