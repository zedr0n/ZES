using System;
using System.Collections.Generic;
using Gridsum.DataflowEx;
using Newtonsoft.Json;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="ICommand" />
    public class Command : MessageEx<CommandStaticMetadata, CommandMetadata>, ICommand
    {
        /// <inheritdoc />
        [JsonIgnore]
        public virtual string Target { get; set; }

        /// <inheritdoc />
        public new ICommandMetadata Metadata
        {
            get => base.Metadata;
            set => base.Metadata = value as CommandMetadata;
        }
        
        /// <inheritdoc />
        public new ICommandStaticMetadata StaticMetadata
        {
            get => base.StaticMetadata;
            set => base.StaticMetadata = value as CommandStaticMetadata;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public bool UseTimestamp 
        { 
            get => StaticMetadata.UseTimestamp;
            set => StaticMetadata.UseTimestamp = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public bool StoreInLog
        {
            get => StaticMetadata.StoreInLog;
            set => StaticMetadata.StoreInLog = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public bool Pure
        {
            get => StaticMetadata.Pure; 
            set => StaticMetadata.Pure = value;
        }

        /// <inheritdoc />
        public ICommand Copy()
        {
            var copy = MemberwiseClone() as Command;
            copy.StaticMetadata = StaticMetadata.Copy();
            copy.Metadata = Metadata.Copy();
            
            // copy.StaticMetadata.MessageId = new MessageId(MessageType, Guid.NewGuid());
            return copy;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var s = $@"
            {{
                Id: {MessageId}
                AncestorId: {AncestorId}
                Timeline: {Timeline}
            }}";
            return s;
        }
    }
}