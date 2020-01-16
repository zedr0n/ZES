using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Pipes
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
        /// <returns>Inner task representing the command processing</returns>
        Task<Task> CommandAsync(ICommand command);

        /// <summary>
        /// Send the query to the bus
        /// </summary>
        /// <param name="query">CQRS query</param>
        /// <typeparam name="TResult">Query result type</typeparam>
        /// <returns>Task representing the asynchronous query processing</returns>
        Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);

        /// <summary>
        /// Pause the command bus queueing the commands
        /// </summary>
        /// <returns>Returns immediately after pausing</returns>
        Task Pause();
        
        /// <summary>
        /// Unpause the command bus dispatching all the queued commands 
        /// </summary>
        /// <returns>Task completes when all the queued commands are dispatched</returns>
        Task Unpause();
    }
}