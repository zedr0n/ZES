using System;
using NodaTime;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Branching
{
    /// <summary>
    /// Timeline tracker 
    /// </summary>
    public interface ITimeline
    {
        /// <summary>
        /// Gets id of the alternate timeline we are in at the moment
        /// </summary>
        /// <value>
        /// Id of the alternate timeline we are in at the moment
        /// </value>
        string Id { get; }

        /// <summary>
        /// Gets current time in the timeline
        /// </summary>
        /// <value>
        /// Current time in the timeline
        /// </value>
        Time Now { get; }

        /// <summary>
        /// Gets a value indicating whether it's a fixed timeline
        /// </summary>
        /// <value>
        /// A value indicating whether it's a fixed timeline
        /// </value>
        bool Live { get; }

        /// <summary>
        /// Gets an observable that emits when the state of pending commands for the timeline changes.
        /// </summary>
        /// <value>
        /// An observable stream of <see cref="ITimeline"/> instances that reflects changes in the timeline's pending commands.
        /// </value>
        public IObservable<ITimeline> PendingCommandsChanged { get; }
        
        /// <summary>
        /// Warp to time
        /// </summary>
        /// <param name="time">Time to warp to</param>
        void Advance(Time time);
        
        /// <summary>
        /// Advances the timeline by the specified period.
        /// </summary>
        /// <param name="period">The duration to advance the timeline by.</param>
        void Advance(Period period);

        /// <summary>
        /// Create new timeline 
        /// </summary>
        /// <param name="id">Timeline id</param>
        /// <param name="time">Null for live or time for fixed timeline</param>
        /// <returns>New timeline</returns>
        ITimeline New(string id, Time time = default);

        /// <summary>
        /// Queues a command for execution on the timeline
        /// </summary>
        /// <param name="command">The command to be queued</param>
        void QueueCommand(ICommand command);

        /// <summary>
        /// Dequeues the next command from the timeline's command queue.
        /// </summary>
        /// <returns>The next command in the queue, or null if the queue is empty.</returns>
        ICommand DequeCommand();

        /// <summary>
        /// Peeks at the next command in the timeline's command queue without removing it.
        /// </summary>
        /// <returns>The next command in the queue, or null if the queue is empty.</returns>
        ICommand PeekCommand();
    }
}