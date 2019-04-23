using System.Collections.Generic;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Sagas
{
    /// <summary>
    /// Event-sourced saga
    /// </summary>
    public interface ISaga : IEventSourced
    {
        /// <summary>
        /// Commands not yet committed 
        /// </summary>
        /// <returns>Commands enumerable</returns>
        IEnumerable<ICommand> GetUncommittedCommands();

        /// <summary>
        /// Add the commands to the dispatch queue
        /// </summary>
        /// <param name="command">Resulting command</param>
        void SendCommand(ICommand command);

        /// <summary>
        /// Process event
        /// </summary>
        /// <param name="e">IEvent</param>
        void When(IEvent e);
    }
}