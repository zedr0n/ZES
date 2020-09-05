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
    public interface ISnapshotEvent : IEvent
    {
        /// <summary>
        /// Gets or sets the event-sourced instance id 
        /// </summary>
        string Id { get; set; }
    }

    /// <inheritdoc />
    public interface ISnapshotEvent<T> : ISnapshotEvent { }
    
    /// <summary>
    /// Saga snapshot event
    /// </summary>
    public interface ISagaSnapshotEvent : ISnapshotEvent { }
}