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
        MessageId CommandId { get; }

        /// <summary>
        /// Gets or sets stream key
        /// </summary>
        string Stream { get; set; }

        /// <summary>
        /// Gets or sets originating stream key
        /// </summary>
        string OriginatingStream { get; set; }
        
        /// <summary>
        /// Create a copy of the event with new guid
        /// </summary>
        /// <returns>Event copy</returns>
        public IEvent Copy();
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