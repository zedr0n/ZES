using System.Collections.Generic;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces.Serialization
{
    /// <summary>
    /// JSON serializer for event/command storage
    /// </summary>
    /// <typeparam name="T"><see cref="ICommand"/>/<see cref="IEvent"/></typeparam>
    public interface ISerializer<T>
        where T : class
    {
        /// <summary>
        /// Serializes the instance to json string
        /// </summary>
        /// <param name="e">Instance to serialize</param>
        /// <returns>JSON serialization string</returns>
        string Serialize(T e);

        /// <summary>
        /// Serialize the event to json data and metadata
        /// </summary>
        /// <param name="e">Instance to serialize</param>
        /// <param name="eventJson">JSON payload</param>
        /// <param name="metadataJson">JSON metadata</param>
        /// <param name="ignoredProperties">Properties to not serialize</param>
        void SerializeEventAndMetadata(T e, out string eventJson, out string metadataJson, IEnumerable<string> ignoredProperties = null);

        /// <summary>
        /// Serialize the metadata to json 
        /// </summary>
        /// <param name="message">Underlying message</param>
        /// <returns>JSON serialized string</returns>
        string EncodeMetadata(T message);
        
        /// <summary>
        /// Deserialize the metadata
        /// </summary>
        /// <param name="json">Serialized json</param>
        /// <returns>Event metadata</returns>
        IEventMetadata DecodeMetadata(string json);

        /// <summary>
        /// Serialize the stream metadata to json 
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>JSON serialized string</returns>
        string EncodeStreamMetadata(IStream stream);

        /// <summary>
        /// Serialize the stream metadata to json 
        /// </summary>
        /// <param name="json">Metadata JSON</param>
        /// <returns>JSON serialized string</returns>
        IStream DecodeStreamMetadata(string json);

        /// <summary>
        /// Deserialize the json string to an object instance
        /// </summary>
        /// <param name="json">Serialized json string</param>
        /// <param name="full">Deserialize the event fully</param>
        /// <returns><see cref="ICommand"/>/<see cref="IEvent"/> instance</returns>
        T Deserialize(string json, bool full = true);
    }
}