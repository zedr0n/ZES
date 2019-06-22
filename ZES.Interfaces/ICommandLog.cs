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
        /// Get the commands corresponding to the stream
        /// </summary>
        /// <param name="stream">Stream info</param>
        /// <returns>Command observable</returns>
        IObservable<ICommand> GetCommands(IStream stream);
    }
}