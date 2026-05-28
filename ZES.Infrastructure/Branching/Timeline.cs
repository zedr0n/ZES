using NodaTime;
using ZES.Infrastructure.Utils;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Clocks;
using IClock = ZES.Interfaces.Clocks.IClock;

namespace ZES.Infrastructure.Branching
{
    /// <inheritdoc />
    public class Timeline : ITimeline
    {
        private readonly IClock _clock;
        private Time _now;
        private readonly string _id;

        /// <inheritdoc />
        public ITimeline ActiveTimeline { get; set; }
        private Timeline This => ActiveTimeline as Timeline ?? this;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        /// <param name="clock">Clock instance</param>
        public Timeline(IClock clock)
        {
            _clock = clock;
            _id = BranchManager.Master;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        /// <param name="id">Branch id</param>
        /// <param name="clock">Logical clock</param>
        /// <param name="time">Fixed time or null for live</param>
        private Timeline(string id, IClock clock, Time time = null)
        {
            _id = id;
            _clock = clock;
            _now = time;
        }

        /// <inheritdoc />
        public bool Live => This._now == null;

        /// <inheritdoc />
        public string Id => This._id; 

        /// <inheritdoc />
        public Time Now => This._now ?? _clock.GetCurrentInstant(); 

        /// <summary>
        /// Create new timeline 
        /// </summary>
        /// <param name="id">Timeline id</param>
        /// <param name="time">Null for live or time for fixed timeline</param>
        /// <returns>New timeline</returns>
        public ITimeline New(string id, Time time = null) => new Timeline(id, _clock, time);

        /// <inheritdoc />
        public void Warp(Time time)
        {
            if (_id == BranchManager.Master)
                return;

            _now = time;
        }

        /// <inheritdoc />
        public void Advance(Period period)
        {
            if (_id == BranchManager.Master)
                return;

            _now = Now.PlusPeriod(period);
        }
    }
}