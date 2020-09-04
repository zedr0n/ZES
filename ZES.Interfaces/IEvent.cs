using System;

namespace ZES.Interfaces
{
    /// <summary>
    /// Base event
    /// </summary>
    public interface IEvent : IEventMetadata
    {
        /// <summary>
        /// Gets originating command id
        /// </summary>
        Guid CommandId { get; }

        /// <summary>
        /// Gets or sets gets originating stream key
        /// </summary>
        /// <value>
        /// Originating stream key
        /// </value>
        string Stream { get; set; }
    }
    
    /// <summary>
    /// Snapshot event
    /// </summary>
    public interface ISnapshotEvent : IEvent { }
}