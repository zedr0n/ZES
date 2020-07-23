using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Encoding extensions for <see cref="SqlStreamStore"/> serialization
    /// </summary>
    public static class EventExtensions
    {
        /// <summary>
        /// Extend the serializer to encode for <see cref="SqlStreamStore"/>
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="e">Event to encode</param>
        /// <returns>Encoded message</returns>
        public static NewStreamMessage Encode(this ISerializer<IEvent> serializer, IEvent e)
        {
            serializer.SerializeEventAndMetadata(e, out var eventJson, out var metadataJson);
            
            // return new NewStreamMessage(e.MessageId, e.MessageType, serializer.Serialize(e), serializer.EncodeMetadata(e));
            return new NewStreamMessage(e.MessageId, e.MessageType, eventJson, metadataJson);
        }
    }
}