using ZES.Infrastructure.Domain;
using ZES.Interfaces;

namespace ZES.Infrastructure.Alerts
{
    /// <inheritdoc cref="IAlert" />
    public class Alert : MessageEx<MessageStaticMetadata, MessageMetadata>, IAlert
    {
        /// <inheritdoc />
        public Alert()
        {
            StaticMetadata.MessageType = GetType().Name;
        }
        
        /// <inheritdoc />
        public new IMessageStaticMetadata StaticMetadata => base.StaticMetadata;

        /// <inheritdoc />
        public new IMessageMetadata Metadata => base.Metadata;
    }
}