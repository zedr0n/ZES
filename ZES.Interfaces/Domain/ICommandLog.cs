using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Log recording all commands in the system
    /// </summary>
    public interface ICommandLog
    {
        /// <summary>
        /// Gets the failed commands
        /// </summary>
        IObservable<HashSet<ICommand>> FailedCommands { get; }
        
        /// <summary>
        /// Record command failing
        /// </summary>
        /// <param name="command">Failed command</param>
        /// <returns>Task representing the record operation</returns>
        Task AddFailedCommand(ICommand command);
        
        /// <summary>
        /// Append the command to the log
        /// </summary>
        /// <param name="command">Command to persist</param>
        /// <returns>Task representing the record operation</returns>
        Task AppendCommand(ICommand command);

        /// <summary>
        /// Get the command originating the event
        /// </summary>
        /// <param name="e">Event resulting from the command</param>
        /// <returns>Command instance if present</returns>
        Task<ICommand> GetCommand(IEvent e);

        /// <summary>
        /// Delete commands pertaining to <paramref name="branchId"/> 
        /// </summary>
        /// <param name="branchId">Branch id</param>
        /// <returns>Completes when branch is deleted</returns>
        Task DeleteBranch(string branchId);
    }
}