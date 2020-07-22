using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Serialization
{
    public abstract class EventSerializerBase<T> : IEventSerializer<T> 
        where T : class, IEvent
    {
        public string EventType => typeof(T).Name;
        public void Write(JsonTextWriter writer, IEvent e) => Write(writer, e as T);
        public abstract void Write(JsonTextWriter writer, T e);
    }
}