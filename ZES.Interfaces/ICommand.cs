using System;

namespace ZES.Interfaces
{
    public interface ICommand
    {
        /// <summary>
        /// Aggregate target id
        /// </summary>
        Guid AggregateId { get; set; }
        /// <summary>
        /// Unix time offset for command timestamp
        /// </summary>
        long Timestamp { get; set; }
    }
}