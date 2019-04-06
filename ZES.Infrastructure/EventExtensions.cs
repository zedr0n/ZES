using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    public static class EventExtensions
    {
        public static NewStreamMessage Encode(this IEventSerializer serializer, IEvent e)
        {
            return new NewStreamMessage(e.EventId, e.EventType, serializer.Serialize(e), serializer.Metadata(e.Timestamp));
        }
    }
}