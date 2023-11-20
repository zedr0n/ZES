using System.Collections.Generic;

namespace ZES.Interfaces.Domain
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
        /// Gets the saga id or returns null if event is not handled by the saga
        /// </summary>
        /// <param name="e">Event to pass to saga</param>
        /// <returns>Saga id or null if saga does not handle this event</returns>
        string SagaId(IEvent e);

        /// <summary>
        /// Process event
        /// </summary>
        /// <param name="e">IEvent</param>
        void When(IEvent e);

        /// <summary>
        /// Converts the input event to saga event
        /// </summary>
        /// <param name="e">Input event</param>
        /// <returns>Saga event</returns>
        IEvent ToSagaEvent(IEvent e);
    }
}