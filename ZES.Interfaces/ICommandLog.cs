using System;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces
{
    /// <summary>
    /// Log recording all commands in the system
    /// </summary>
    public interface ICommandLog
    {
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