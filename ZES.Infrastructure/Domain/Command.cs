using System.Linq;
using Newtonsoft.Json;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="ICommand" />
    public class Command : Message<CommandStaticMetadata, CommandMetadata>, ICommand
    {
        private bool _ephemeral;
        
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
        public string Guid
        {
            get;
            set
            {
                if (value == null)
                    return;
                field = value;
                Metadata.MessageId = new MessageId(MessageId.MessageType, System.Guid.Parse(value));
            }
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
            get { return _ephemeral; }
            set
            {
                _ephemeral = value;
                if(_ephemeral)
                    StoreInLog = false;
            }
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
                Id: {string.Concat(MessageId.ToString().Where(x => !char.IsWhiteSpace(x)))},
                AncestorId: {AncestorId}
                Timeline: {Timeline}
            }}";
            return s;
        }
    }
}