using System.Collections.Generic;
using NodaTime;
using ZES.Infrastructure.Utils;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using IClock = ZES.Interfaces.Clocks.IClock;

namespace ZES.Infrastructure.Branching
{
    /// <inheritdoc />
    public class Timeline : ITimeline
    {
        private readonly IClock _clock;
        private Time _now;
        private readonly Queue<ICommand> _pendingCommands = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        /// <param name="clock">Clock instance</param>
        public Timeline(IClock clock)
        {
            _clock = clock;
            Id = BranchManager.Master;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        /// <param name="id">Branch id</param>
        /// <param name="clock">Logical clock</param>
        /// <param name="time">Fixed time or null for live</param>
        private Timeline(string id, IClock clock, Time time = null)
        {
            Id = id;
            _clock = clock;
            _now = time;
        }

        /// <inheritdoc />
        public bool Live => _now == null;

        /// <inheritdoc />
        public string Id { get; }

        /// <inheritdoc />
        public Time Now => _now ?? _clock.GetCurrentInstant(); 

        /// <summary>
        /// Create new timeline 
        /// </summary>
        /// <param name="id">Timeline id</param>
        /// <param name="time">Null for live or time for fixed timeline</param>
        /// <returns>New timeline</returns>
        public ITimeline New(string id, Time time = null) => new Timeline(id, _clock, time);

        /// <inheritdoc />
        public void QueueCommand(ICommand command)
        {
            _pendingCommands.Enqueue(command);
        }

        /// <inheritdoc />
        public ICommand DequeCommand()
        {
            return _pendingCommands.Count > 0 ? _pendingCommands.Dequeue() : null;
        }

        /// <inheritdoc />
        public ICommand PeekCommand()
        {
            return _pendingCommands.Count > 0 ? _pendingCommands.Peek() : null;    
        }

        /// <inheritdoc />
        public void Warp(Time time)
        {
            if (Id == BranchManager.Master)
                return;

            _now = time;
        }

        /// <inheritdoc />
        public void Advance(Period period)
        {
            if (Id == BranchManager.Master)
                return;

            _now = Now.PlusPeriod(period);
        }
    }
}