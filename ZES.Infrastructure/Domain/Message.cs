using System;
using System.Collections.Generic;
using System.Text;
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
        
        /// <summary>
        /// Gets or sets a value indicating whether the message is in a temporary stream.
        /// </summary>
        [JsonIgnore]
        public bool InTemporaryStream { get; set; }

        /// <inheritdoc />
        [JsonIgnore]
        public string Timeline
        {
            get => Metadata.Timeline;
            set
            {
                var timeline = Metadata.Timeline;
                if (timeline == value)
                    return;

                // Try in-place update of cached JSON
                var json = Metadata.Json;
                if (json != null && timeline != null)
                {
                    if (TryReplaceStringInJson(ref json, "\"Timeline\":", timeline, value, InTemporaryStream))
                    {
                        Metadata.Json = json;
                        Metadata.Timeline = value;
                        return;
                    }
                }

                Metadata.Json = null;
                Metadata.Timeline = value;
            }
        }

        /// <summary>
        /// Attempts to replace a string value in a JSON-formatted string for a given property name.
        /// </summary>
        /// <param name="json">The JSON string, passed by reference, in which the replacement will be attempted.</param>
        /// <param name="prefix">String prefix</param>
        /// <param name="oldValue">The existing value of the property to be replaced.</param>
        /// <param name="newValue">The new value to replace the old value for the property.</param>
        /// <param name="isTemporaryStream"></param>
        /// <returns>
        /// True if the replacement was successful; otherwise, false.
        /// </returns>
        protected static bool TryReplaceStringInJson(ref string json, string prefix, string oldValue, string newValue, bool isTemporaryStream)
        {
            if (!Configuration.ReplaceInMetadata)
                return false;

            if (isTemporaryStream)
                return true;

            // Pattern: "PropertyName": oldValue, or "PropertyName":oldValue, depending on formatting
            // Use string.Concat to reduce allocations
            var oldPattern = string.Concat(prefix, oldValue);
            //var oldPattern = string.Concat("\"", name, "\":", space, oldValue);

            //var newPattern = string.Concat("\"", name, "\":", space, newValue);
            var newPattern = string.Concat(prefix, newValue);
            json = json.Replace(oldPattern, newPattern);
            return true;
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