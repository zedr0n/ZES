using System;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    public class Timeline : ITimeline
    {
        private long _now;
        
        public string Id { get; set; } = TimeTraveller.Master;

        public long Now
        {
            get => Id == TimeTraveller.Master ? DateTime.UtcNow.Ticks : _now;
            set => _now = value;
        }
    }
}