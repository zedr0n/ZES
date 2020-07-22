using Newtonsoft.Json;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;

namespace ZES.Infrastructure.Serialization
{
    /// <summary>
    /// Explicit JSON event deserializer 
    /// </summary>
    public interface IEventDeserializer
    {
        /// <summary>
        /// Gets the event type 
        /// </summary>
        string EventType { get; }
                        
        /// <summary>
        /// Explicit parser switch
        /// </summary>
        /// <param name="reader">JSON reader</param>
        /// <param name="currentProperty">Current property being processed</param>
        /// <param name="e">Output event</param>
        void Switch(JsonTextReader reader, string currentProperty, Event e);
        
        /// <summary>
        /// Create typed event
        /// </summary>
        /// <returns>Typed event</returns>
        Event Create();
    }

    /// <inheritdoc />
    public interface IEventDeserializer<in T> : IEventDeserializer
        where T : Event
    {
        /// <summary>
        /// Explicit parser switch
        /// </summary>
        /// <param name="reader">JSON reader</param>
        /// <param name="currentProperty">Current property being processed</param>
        /// <param name="e">Output event</param>
        void Switch(JsonTextReader reader, string currentProperty, T e);
    }

    /// <summary>
    /// Events explicit serialization
    /// </summary>
    public interface IEventSerializationRegistry
    {
        /// <summary>
        /// Gets the deserializer corresponding to the serialized event
        /// </summary>
        /// <param name="payload">JSON data</param>
        /// <returns>Deserializer instance</returns>
        IEventDeserializer GetDeserializer(string payload);

        /// <summary>
        /// Gets the serializer corresponding to the input event
        /// </summary>
        /// <param name="e">Input event</param>
        /// <returns>Serializer instance</returns>
        IEventSerializer GetSerializer(IEvent e);
    }
}