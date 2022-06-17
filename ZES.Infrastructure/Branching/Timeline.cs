using System;
using NodaTime;
using NodaTime.Extensions;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;
using IClock = ZES.Interfaces.Clocks.IClock;

namespace ZES.Infrastructure.Branching
{
    /// <inheritdoc />
    public class Timeline : ITimeline
    {
        private readonly IClock _clock;
        private Time _now;

        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        /// <param name="clock">Clock instance</param>
        public Timeline(IClock clock)
        {
            _clock = clock;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        /// <param name="id">Branch id</param>
        /// <param name="clock">Logical clock</param>
        /// <param name="time">Fixed time or null for live</param>
        private Timeline(string id, IClock clock, Time time = default)
        {
            Id = id;
            _clock = clock;
            _now = time;
        }

        /// <inheritdoc />
        public bool Live => _now == default;

        /// <inheritdoc />
        public string Id { get; set; } = BranchManager.Master;

        /// <inheritdoc />
        public Time Now => _now ?? _clock.GetCurrentInstant(); 

        /// <summary>
        /// Create new timeline 
        /// </summary>
        /// <param name="id">Timeline id</param>
        /// <param name="time">Null for live or time for fixed timeline</param>
        /// <returns>New timeline</returns>
        public ITimeline New(string id, Time time = default) => new Timeline(id, _clock, time);    
        
        /// <inheritdoc />
        public void Set(ITimeline rhs)
        {
            var t = rhs as Timeline;
            if (t == null)
                throw new InvalidOperationException();

            Id = t.Id;
            _now = t._now;
        }

        /// <inheritdoc />
        public void Warp(Time time)
        {
            if (Id == BranchManager.Master)
                return;

            _now = time;
        }
    }
}