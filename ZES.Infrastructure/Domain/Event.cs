using System;
using Gridsum.DataflowEx;
using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="IEvent"/>
    public class Event : Message<EventStaticMetadata, EventMetadata>, IEvent
    {
        /// <inheritdoc />
       [JsonProperty(Order = -2)]
        public new IEventMetadata Metadata
        {
            get => base.Metadata;
            set => base.Metadata = value as EventMetadata;
        }

        /// <inheritdoc />
        [JsonProperty(Order = -2)]
        public new IEventStaticMetadata StaticMetadata
        {
            get => base.StaticMetadata;
            set => base.StaticMetadata = value as EventStaticMetadata;
        } 

        /// <inheritdoc />
        public virtual IEvent Copy()
        {
            //var copy = new Event();
            var copy = MemberwiseClone() as Event;
            copy.StaticMetadata = StaticMetadata.Copy();
            copy.Metadata = Metadata.Copy();
            
            copy.Metadata.MessageId = new MessageId(MessageType, Guid.NewGuid());
            copy.Metadata.Json = null;
            copy.StaticMetadata.AncestorId = StaticMetadata.AncestorId ?? Metadata.MessageId;
            
            copy.StaticMetadata.OriginatingStream = Metadata.Stream;
            copy.StaticMetadata.CommandId = StaticMetadata.CommandId;
            return copy;
        }
        
        /// <inheritdoc />
        public virtual IEvent CopyPayload()
        {
            //var copy = new Event();
            var copy = MemberwiseClone() as Event;
            copy.StaticMetadata = null;
            copy.Metadata = null;
            return copy;
        }


        /// <inheritdoc />
        [JsonIgnore]
        public MessageId CommandId
        {
            get => StaticMetadata.CommandId;
            set => StaticMetadata.CommandId = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public string OriginatingStream => StaticMetadata.OriginatingStream;

        /// <inheritdoc />
        [JsonIgnore]
        public int Version
        {
            get => Metadata.Version;
            set
            {
                Metadata.Json = null;
                Metadata.Version = value;
            }
        }

        /// <inheritdoc />
        [JsonIgnore]
        public string Stream
        {
            get => Metadata.Stream;
            set
            {
                Metadata.Json = null;
                Metadata.Stream = value;
            }
        }

        /// <inheritdoc />
        [JsonIgnore]
        public string StreamHash
        {
            get => Metadata.StreamHash;
            set
            {
                Metadata.Json = null;
                Metadata.StreamHash = value;
            }
        }

        /// <inheritdoc />
        [JsonIgnore]
        public string ContentHash
        {
            get => Metadata.ContentHash;
            set
            {
                Metadata.Json = null;
                Metadata.ContentHash = value;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var s = $@"
            {{
                Id: {MessageId}
                Version: {Version}
                Stream: {Stream}
                Timeline: {Timeline}
                AncestorId: {AncestorId}
            }}";
            return s;
        }
    }
    /// <inheritdoc cref="IEvent"/>
    public class Event<TPayload> : Event, IEvent<TPayload>
        where TPayload : class, new()
    {
        /// <inheritdoc />
        public TPayload Payload { get; set; }
    }
}