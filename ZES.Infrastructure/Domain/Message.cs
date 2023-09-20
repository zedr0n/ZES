using System;
using Gridsum.DataflowEx;
using Newtonsoft.Json;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class Message<TStaticMetadata, TMetadata> : IMessage<TStaticMetadata, TMetadata> 
        where TStaticMetadata : class, IMessageStaticMetadata, new()
        where TMetadata : IMessageMetadata, new()
    {
        /// <summary>
        /// Creates a new instance of <see cref="Message{TStaticMetadata,TMetadata}"/>
        /// </summary>
        public Message()
        {
            StaticMetadata.MessageType = GetType().GetFriendlyName();
            Metadata.MessageId = new MessageId(MessageType, Guid.NewGuid());
        }
        
        /// <inheritdoc />
        public TMetadata Metadata { get; protected set; } = new();
        
        /// <inheritdoc />
        public TStaticMetadata StaticMetadata { get; protected set; } = new();

        /// <inheritdoc />
        [JsonIgnore]
        public MessageId MessageId => Metadata.MessageId;
        
        /// <inheritdoc />
        [JsonIgnore]
        public string MessageType => StaticMetadata.MessageType;

        /// <inheritdoc />
        [JsonIgnore]
        public MessageId AncestorId
        {
            get => StaticMetadata.AncestorId;
            set => StaticMetadata.AncestorId = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public string CorrelationId
        {
            get => StaticMetadata.CorrelationId;
            set => StaticMetadata.CorrelationId = value;
        }
        
        /// <inheritdoc />
        [JsonIgnore]
        public MessageId RetroactiveId
        {
            get => StaticMetadata.RetroactiveId; 
            set => StaticMetadata.RetroactiveId = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public EventId LocalId
        {
            get => StaticMetadata.LocalId;
            set => StaticMetadata.LocalId = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public EventId OriginId
        {
            get => StaticMetadata.OriginId;
            set => StaticMetadata.OriginId = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public Time Timestamp
        {
            get => Metadata.Timestamp;
            set
            {
                Metadata.Json = null;
                Metadata.Timestamp = value;
            }
        }

        /// <inheritdoc />
        [JsonIgnore]
        public string Timeline
        {
            get => Metadata.Timeline;
            set
            {
                Metadata.Json = null;
                Metadata.Timeline = value;
            }
        }

        /// <inheritdoc />
        [JsonIgnore]
        public string MetadataJson
        {
            get => Metadata.Json;
            set => Metadata.Json = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public string StaticMetadataJson 
        { 
            get => StaticMetadata.Json; 
            set => StaticMetadata.Json = value; 
        }
        
        /// <inheritdoc />
        [JsonIgnore]
        public string Json { get; set; }

        /// <inheritdoc />
        public void CopyMetadata(IMessage other)
        {
            var message = other as Message<TStaticMetadata, TMetadata>;
            Metadata = message.Metadata;
        }
    }
    
    /// <inheritdoc cref="IMessage{TStaticMetadata,TMetadata}"/>
    public abstract class Message<TStaticMetadata, TMetadata, TPayload> : Message<TStaticMetadata, TMetadata>, IMessage<TStaticMetadata, TMetadata, TPayload> 
        where TStaticMetadata : class, IMessageStaticMetadata, new()
        where TMetadata : IMessageMetadata, new()
        where TPayload : class, new()
    {
        /// <inheritdoc />
        public TPayload Payload { get; set; }
    }
}