using System;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class Timeline : ITimeline
    {
        private long? _now;

        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        public Timeline()
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        /// <param name="id">Branch id</param>
        /// <param name="time">Fixed time or null for live</param>
        private Timeline(string id, long? time = null)
        {
            Id = id;
            _now = time;
        }

        /// <inheritdoc />
        public bool Live => _now == null;

        /// <summary>
        /// Create new timeline 
        /// </summary>
        /// <param name="id">Timeline id</param>
        /// <param name="time">Null for live or time for fixed timeline</param>
        /// <returns>New timeline</returns>
        public static Timeline New(string id, long? time = null) => new Timeline(id, time);    
        
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
        public string Id { get; set; } = BranchManager.Master;

        /// <inheritdoc />
        public long Now => _now ?? DateTime.UtcNow.Ticks;
    }
}