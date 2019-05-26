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
            return new NewStreamMessage(e.MessageId, e.EventType, serializer.Serialize(e), serializer.EncodeMetadata(e));
        }
    }
}