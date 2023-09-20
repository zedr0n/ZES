using System;

namespace ZES.Interfaces
{
    /// <summary>
    /// Base event with payload
    /// </summary>
    public interface IEvent : IMessage<IEventStaticMetadata, IEventMetadata>
    {
        /// <inheritdoc cref="IEventStaticMetadata.CommandId"/>
        MessageId CommandId { get; set; }
        
        /// <inheritdoc cref="IEventStaticMetadata.OriginatingStream"/>
        string OriginatingStream { get; }
        
        /// <inheritdoc cref="IEventMetadata.Version"/>
        int Version { get; set; }
        
        /// <inheritdoc cref="IEventMetadata.Stream"/>
        string Stream { get; set; }

        /// <inheritdoc cref="IEventMetadata.StreamHash"/>
        string StreamHash { get; set; }
        
        /// <inheritdoc cref="IEventMetadata.ContentHash"/>
        string ContentHash { get; set; }

        /// <summary>
        /// Create a copy of the event with new guid
        /// </summary>
        /// <returns>Event copy</returns>
        public IEvent Copy();
        
        /// <summary>
        /// Create a copy of the event with new guid
        /// </summary>
        /// <returns>Event copy</returns>
        public IEvent CopyPayload();
    }
    
    /// <summary>
    /// Base event with payload
    /// </summary>
    /// <typeparam name="TPayload">Payload type</typeparam>
    public interface IEvent<TPayload> : IEvent, IMessage<IEventStaticMetadata, IEventMetadata, TPayload>
        where TPayload : class, new()
    {
    }

    /// <summary>
    /// Snapshot event
    /// </summary>
    public interface ISnapshotEventEx : IEvent
    {
        /// <summary>
        /// Gets or sets the event-sourced instance id 
        /// </summary>
        string Id { get; set; }
    }

    /// <inheritdoc />
    public interface ISnapshotEventEx<T> : ISnapshotEventEx
    {
    }

    /// <summary>
    /// Saga snapshot event
    /// </summary>
    public interface ISagaSnapshotEventEx : ISnapshotEventEx
    {
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