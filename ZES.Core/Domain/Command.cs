using System;
using ZES.Interfaces;

namespace ZES.Core.Domain
{
    public class Command : ICommand
    {
        public Guid AggregateId { get; set; }
        public long Timestamp { get; set; }
    }
}