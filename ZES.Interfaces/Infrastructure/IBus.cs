using System.Collections.Generic;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Infrastructure
{
    /// <summary>
    /// Application command and query bus
    /// </summary>
    public interface IBus
    {
        // <remarks>Command will only be processed once ( based on object's hash code )</remarks>

        /// <summary>
        /// Send the command to the bus
        /// </summary>
        /// <param name="command">CQRS command</param>
        /// <param name="waitForRetroactive">Wait for retroactive commands to complete before launching the next one</param>
        /// <returns>Inner task representing the command processing</returns>
        Task<Task> CommandAsync(ICommand command, bool waitForRetroactive = false);

        /// <summary>
        /// Sends a batch of commands to the bus for processing.
        /// </summary>
        /// <param name="commands">Array of CQRS commands to be processed.</param>
        /// <returns>A task representing the asynchronous execution of the command batch.</returns>
        Task<Task> CommandBatchAsync(IEnumerable<ICommand> commands);

        /// <summary>
        /// Send the query to the bus
        /// </summary>
        /// <param name="query">CQRS query</param>
        /// <typeparam name="TResult">Query result type</typeparam>
        /// <returns>Task representing the asynchronous query processing</returns>
        Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);

        /// <summary>
        /// Executes a command using the bus with specified retry attempts and an optional wait for retroactive commands.
        /// </summary>
        /// <param name="command">The command to be executed.</param>
        /// <param name="nRetries">The number of retry attempts in case of failure.</param>
        /// <param name="waitForRetroactive">Specifies whether to wait for retroactive commands to complete before execution.</param>
        /// <returns>A task representing the result of the command execution, returning true if the command succeeded.</returns>
        Task<bool> Command(ICommand command, int nRetries = 0, bool waitForRetroactive = false);
    }
}