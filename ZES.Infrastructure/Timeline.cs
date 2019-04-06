using System;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    
    public class Timeline : ITimeline
    {
        public string Id { get; } = "master";
        public long Now => Id != "master" ? _now : DateTime.UtcNow.Ticks;
        private long _now;

        public void Set(long now)
        {
            if (Id != "master")
                _now = now;
        }
    }
}