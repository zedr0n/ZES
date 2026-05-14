using System.Linq;
using Newtonsoft.Json;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="ICommand" />
    public class Command : Message<CommandStaticMetadata, CommandMetadata>, ICommand
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
        public virtual string Guid
        {
            get;
            set
            {
                field = value;
                Metadata.MessageId = value != null
                    ? new MessageId(MessageId.MessageType, System.Guid.Parse(value))
                    : new MessageId(MessageId.MessageType, System.Guid.NewGuid());            }
        } = null;

        /// <inheritdoc />
        [JsonIgnore]
        public bool StoreInLog
        {
            get => StaticMetadata.StoreInLog;
            set => StaticMetadata.StoreInLog = value;
        }
        
        /// <inheritdoc />
        [JsonIgnore]
        public bool Pure { get; set; }

        /// <inheritdoc />
        [JsonIgnore]
        public bool Ephemeral
        {
            get;
            set
            {
                field = value;
                if(value)
                    StoreInLog = false;
            }
        }

        /// Indicates whether the command execution has failed.
        /// When set to true, it signals that the corresponding command encountered an error or was unsuccessful.
        /// The property implementation can vary based on the context. In derived classes, additional logic may be used
        /// to determine failure, such as aggregating the failure state from other related commands.
        [JsonIgnore]
        public virtual bool Failed { get; set; }

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
                Id: {string.Concat(MessageId.ToString().Where(x => !char.IsWhiteSpace(x)))},
                AncestorId: {AncestorId}
                Timeline: {Timeline}
            }}";
            return s;
        }
    }
}