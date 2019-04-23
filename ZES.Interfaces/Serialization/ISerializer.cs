using ZES.Interfaces.Domain;

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
        /// Serialize the metadata to json 
        /// </summary>
        /// <param name="timestamp">Timestamp</param>
        /// <returns>JSON serialized string</returns>
        string Metadata(long? timestamp);
        
        /// <summary>
        /// Deserialize the json string to an object instance
        /// </summary>
        /// <param name="json">Serialized json string</param>
        /// <returns><see cref="ICommand"/>/<see cref="IEvent"/> instance</returns>
        T Deserialize(string json);
    }
}