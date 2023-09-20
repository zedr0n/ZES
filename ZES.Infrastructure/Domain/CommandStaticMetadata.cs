using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Base command static metadata
    /// </summary>
    public class CommandStaticMetadata : MessageStaticMetadata, ICommandStaticMetadata
    {
        /// <inheritdoc />
        public bool UseTimestamp { get; set; } = false;

        /// <inheritdoc />
        public bool StoreInLog { get; set; } = true;
        
        /// <inheritdoc />
        public ICommandStaticMetadata Copy()
        {
            var copy = MemberwiseClone() as CommandStaticMetadata;
            // copy.MessageId = MessageId;
            // copy.AncestorId = AncestorId;
            return copy;
        }
    } 
}
