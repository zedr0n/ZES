using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
        private readonly SortedDictionary<Time, Queue<ICommand>> _pendingCommands = new();

        private readonly Subject<ITimeline> _pendingCommandsChanged = new();

        /// <inheritdoc />
        public IObservable<ITimeline> PendingCommandsChanged => _pendingCommandsChanged.AsObservable();

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
            lock (_pendingCommands)
            {
                if (!_pendingCommands.TryGetValue(command.Timestamp, out var queue))
                {
                    queue = new Queue<ICommand>();
                    _pendingCommands.Add(command.Timestamp, queue);
                }
                queue.Enqueue(command);
            }
            _pendingCommandsChanged.OnNext(this);
        }

        /// <inheritdoc />
        public ICommand DequeCommand()
        {
            lock (_pendingCommands)
            {
                if (_pendingCommands.Count == 0)
                    return null;
                
                var first = _pendingCommands.First();
                var command = first.Value.Dequeue();

                if (first.Value.Count == 0)
                    _pendingCommands.Remove(first.Key);

                return command;
            }
        }

        /// <inheritdoc />
        public ICommand PeekCommand()
        {
            lock (_pendingCommands)
            {
                if (_pendingCommands.Count == 0)
                    return null;
                
                return _pendingCommands.First().Value.Peek();
            }
        }

        /// <inheritdoc />
        public void Advance(Time time)
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