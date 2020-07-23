using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Serialization
{
    /// <summary>
    /// Explicit JSON event serializer 
    /// </summary>
    public interface IEventSerializer
    {
        /// <summary>
        /// Gets the associated event type
        /// </summary>
        string EventType { get; }
        
        /// <summary>
        /// Serialize the explicit properties of the final instance
        /// </summary>
        /// <param name="writer">JSON writer</param>
        /// <param name="e">Instance to serialize</param>
        void Write(JsonTextWriter writer, IEvent e);
    }

    /// <inheritdoc />
    public interface IEventSerializer<T> : IEventSerializer
        where T : class, IEvent
    {
    }
}