using Newtonsoft.Json;
using ZES.Infrastructure.Domain;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public abstract class EventDeserializerBase<T> : IEventDeserializer<T>
        where T : Event, new()
    {
        /// <inheritdoc />
        public virtual string EventType => typeof(T).FullName + "," + typeof(T).Assembly.FullName.Split(',')[0];

        /// <inheritdoc />
        public void Switch(JsonTextReader reader, string currentProperty, Event e) =>
            Switch(reader, currentProperty, e as T);

        /// <inheritdoc />
        public Event Create() => new T();

        /// <inheritdoc />
        public virtual bool SupportsPayload => true;

        /// <inheritdoc />
        public abstract void Switch(JsonTextReader reader, string currentProperty, T e);
    }

    /// <summary>
    /// Default event deserializer
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DefaultEventDeserializer<T> : EventDeserializerBase<T>
        where T : Event, new()
    {
        /// <inheritdoc />
        public override void Switch(JsonTextReader reader, string currentProperty, T e)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc />
        public override bool SupportsPayload => false;
    }
}