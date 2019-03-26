using System;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    public class Event : IEvent
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; }
        public long Timestamp { get; set; }
        public int Version { get; set; }
        public string Stream { get; set; }
    }
}