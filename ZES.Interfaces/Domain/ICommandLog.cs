using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZES.Interfaces.EventStore;

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
        /// Gets the command on the branch
        /// </summary>
        /// <param name="branchId">Branch id</param> 
        /// <returns>List of commands</returns>
        Task<IEnumerable<ICommand>> GetCommands(string branchId);

        /// <summary>
        /// Delete commands pertaining to <paramref name="branchId"/> 
        /// </summary>
        /// <param name="branchId">Branch id</param>
        /// <returns>Completes when branch is deleted</returns>
        Task DeleteBranch(string branchId);

        /// <summary>
        /// List the command log streams
        /// </summary>
        /// <param name="branchId">Branch id</param>
        /// <returns>List of command log streams</returns>
        Task<IEnumerable<IStream>> ListStreams(string branchId);

        /// <summary>
        /// Read specified number of events from the stream forward from starting version 
        /// </summary>
        /// <param name="stream">Target stream</param>
        /// <param name="start">Starting version for the read</param>
        /// <param name="count">Number of events to read</param>
        /// <returns>Cold observable of read events</returns>
        IObservable<ICommand> ReadStream(IStream stream, int start, int count = -1);

        /// <summary>
        /// Get the stream corresponding to the command
        /// </summary>
        /// <param name="c">Command instance</param>
        /// <param name="branchId">Branch id</param>
        /// <returns>Associated stream</returns>
        IStream GetStream(ICommand c, string branchId = null);

        /// <summary>
        /// Gets the command from the command log if it exists
        /// </summary>
        /// <param name="c">Command instance</param>
        /// <returns>Command instance or default if it doesn't exists</returns>
        Task<ICommand> GetCommand(ICommand c);
    }
}