using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Base command metadata class
    /// </summary>
    public class CommandMetadata : MessageMetadata, ICommandMetadata
    {
        /// <inheritdoc />
        public ICommandMetadata Copy()
        {
            var copy = MemberwiseClone() as CommandMetadata;
            return copy;
        }
    }    
}
