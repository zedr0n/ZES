using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Serialization
{
    public class EventSerializer : Serializer<IEvent>, IEventSerializer { }
}
