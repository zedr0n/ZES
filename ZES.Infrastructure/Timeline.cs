using System;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    public class Timeline : ITimeline
    {
        public string TimelineId { get; }
        public long Now => TimelineId != null ? _now : DateTime.UtcNow.Ticks;
        private long _now;

        public void Set(long now)
        {
            if (TimelineId != null)
                _now = now;
        }
    }
}