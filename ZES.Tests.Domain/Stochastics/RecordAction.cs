using System.Collections.Generic;
using ZES.Infrastructure.Stochastics;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Commands;

namespace ZES.Tests.Domain.Stochastics
{
    /// <inheritdoc />
    public class RecordAction : BranchAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecordAction"/> class.
        /// </summary>
        /// <param name="manager">Branch manager</param>
        /// <param name="bus">Command bus</param>
        public RecordAction(IBranchManager manager, IBus bus) 
            : base(manager, bus)
        {
        }

        /// <inheritdoc/>
        protected override IEnumerable<IEnumerable<ICommand>> GetCommands(BranchState current)
        {
            return new[]
            {
                new ICommand[] { new CreateRecord(Id), new AddRecord(Id, 1.0) }, 
            };
        }
    }
}