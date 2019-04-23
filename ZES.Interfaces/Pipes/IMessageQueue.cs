using System;

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
        /// Submit the event to the message queue
        /// </summary>
        /// <param name="e">Event instance</param>
        void Event(IEvent e);
        
        /// <summary>
        /// Submit the alert to the message queue
        /// </summary>
        /// <param name="alert">Alert instance</param>
        void Alert(IAlert alert);
    }
}