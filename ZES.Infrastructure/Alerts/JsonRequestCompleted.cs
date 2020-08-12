using ZES.Interfaces.Net;

namespace ZES.Infrastructure.Alerts
{
    /// <summary>
    /// Json request result
    /// </summary>
    public class JsonRequestCompleted : Alert
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRequestCompleted"/> class.
        /// </summary>
        /// <param name="requestorId">Request correlation id</param>
        /// <param name="url">JSON endpoint url</param>
        /// <param name="jsonData">JSON data result</param>
        public JsonRequestCompleted(string requestorId, string url, string jsonData)
        {
            Url = url;
            JsonData = jsonData;
            RequestorId = requestorId;
        }

        /// <summary>
        /// Gets the request correlation id
        /// </summary>
        public string RequestorId { get; }
        
        /// <summary>
        /// Gets the json data payload
        /// </summary>
        public string JsonData { get; }
        
        /// <summary>
        /// Gets the json endpoint url 
        /// </summary>
        public string Url { get; }
    }

    /// <summary>
    /// Json request deserialized result
    /// </summary>
    /// <typeparam name="T">Deserialized type</typeparam>
    public class JsonRequestCompleted<T> : Alert
        where T : class, IJsonResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRequestCompleted{T}"/> class.
        /// </summary>
        /// <param name="requestorId">Request correlation id</param>
        /// <param name="url">JSON endpoint url</param>
        /// <param name="data">Deserialized result</param>
        public JsonRequestCompleted(string requestorId, string url, T data)
        {
            Url = url;
            Data = data;
            RequestorId = requestorId;
        }
        
        /// <summary>
        /// Gets the request correlation id
        /// </summary>
        public string RequestorId { get; }

        /// <summary>
        /// Gets the deserialized result
        /// </summary>
        public T Data { get; }
        
        /// <summary>
        /// Gets the json endpoint url 
        /// </summary>
        public string Url { get; }
    }
}