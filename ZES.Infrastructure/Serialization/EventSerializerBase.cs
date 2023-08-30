using Newtonsoft.Json;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;

namespace ZES.Infrastructure.Serialization
{
    /// <inheritdoc />
    public abstract class EventSerializerBase<T> : IEventSerializer<T> 
        where T : class, IEvent
    {
        /// <inheritdoc />
        public string EventType => typeof(T).FullName;

        /// <inheritdoc />
        public void Write(JsonTextWriter writer, IEvent e) => Write(writer, e as T);
        
        /// <summary>
        /// Serialize the explicit properties of the final instance
        /// </summary>
        /// <param name="writer">JSON writer</param>
        /// <param name="e">Instance to serialize</param>
        public abstract void Write(JsonTextWriter writer, T e);
    }
}