using Newtonsoft.Json;
using ZES.Infrastructure.Domain;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public abstract class EventDeserializerBase<T> : IEventDeserializer<T>
        where T : Event, new()
    {
        /// <inheritdoc />
        public virtual string EventType => typeof(T).FullName;

        /// <inheritdoc />
        public void Switch(JsonTextReader reader, string currentProperty, Event e) =>
            Switch(reader, currentProperty, e as T);

        /// <inheritdoc />
        public Event Create() => new T();

        /// <inheritdoc />
        public abstract void Switch(JsonTextReader reader, string currentProperty, T e);
    }
}