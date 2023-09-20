using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;

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
        /// Gets the flag indicating whether retroactive execution is active
        /// </summary>
        IObservable<bool> RetroactiveExecution { get; }

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

        /// <summary>
        /// Mark the command as completed
        /// </summary>
        /// <param name="commandId">Command id</param>
        /// <returns>Completes when the counter is updated</returns>
        Task CompleteCommand(MessageId commandId);

        /// <summary>
        /// Mark the command as uncompleted
        /// </summary>
        /// <param name="commandId">Command id</param>
        /// <param name="isRetroactive">True if command is retroactive</param>
        /// <returns>Completes when counter is updated</returns>
        Task UncompleteCommand(MessageId commandId, bool isRetroactive = false);
        
        /// <summary>
        /// Current command state
        /// </summary>
        /// <param name="commandId">Command id</param>
        /// <returns>Command state observable</returns>
        IObservable<CommandState> CommandState(MessageId commandId);

        /// <summary>
        /// Sets the command state to <see cref="CommandState.Failed"/>
        /// </summary>
        /// <param name="commandId">Command id</param>
        /// <returns>Completes when state is updated</returns>
        Task FailCommand(MessageId commandId);
    }
}