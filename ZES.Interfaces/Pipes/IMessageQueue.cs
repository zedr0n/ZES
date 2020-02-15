using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Pipes
{
    /// <summary>
    /// Message queue
    /// ( used for sagas and services )
    /// </summary>
    public interface IMessageQueue
    {
        /// <summary>
        /// Gets the hot observable representing the live events in the system
        /// </summary>
        /// <value>
        /// The hot observable representing the live events in the system
        /// </value>
        IObservable<IEvent> Messages { get; }

        /// <summary>
        /// Gets the hot observable representing the alerts in the system
        /// </summary>
        /// <value>
        /// The hot observable representing the alerts in the system
        /// </value>
        IObservable<IAlert> Alerts { get; }

        /// <summary>
        /// Gets the number of currently uncompleted messages
        /// </summary>
        IObservable<int> UncompletedMessages { get; }

        /// <summary>
        /// Submit the event to the message queue
        /// </summary>
        /// <param name="e">Event instance</param>
        void Event(IEvent e);
        
        /// <summary>
        /// Submit the alert to the message queue
        /// </summary>
        /// <param name="alert">Alert instance</param>
        void Alert(IAlert alert);

        /// <summary>
        /// Mark the message as completed
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>Completes when counter is updated</returns>
        Task CompleteMessage(IMessage message);

        /// <summary>
        /// Mark the message as uncompleted
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>Completes when counter is updated</returns>
        Task UncompleteMessage(IMessage message);

        /// <summary>
        /// Uncompleted messages on specific branch
        /// </summary>
        /// <param name="branchId">Branch id</param>
        /// <returns>Number of currently uncompleted messages on branch</returns>
        IObservable<int> UncompletedMessagesOnBranch(string branchId);
    }
}