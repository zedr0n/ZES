using System;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    public class Event : IEvent
    {
        protected Event()
        {
            EventType = GetType().Name;
            EventId = Guid.NewGuid();
        }
        
        public Guid EventId { get; }
        public string EventType { get; }
        public long Timestamp { get; set; }
        public int Version { get; set; }
        public string Stream { get; set; }
    }
}