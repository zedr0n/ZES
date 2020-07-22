using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Serialization
{
    /// <summary>
    /// Explicit JSON event serializer 
    /// </summary>
    public interface IEventSerializer
    {
        string EventType { get; }
        void Write(JsonTextWriter writer, IEvent e);
    }

    public interface IEventSerializer<T> : IEventSerializer
        where T : class, IEvent
    {
    }
}