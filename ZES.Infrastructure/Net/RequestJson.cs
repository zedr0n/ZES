using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Net;

namespace ZES.Infrastructure.Net
{
    /// <summary>
    /// Request json side effect command
    /// </summary>
    public class RequestJson : Command, ISideEffectCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestJson"/> class.
        /// </summary>
        /// <param name="url">Target url</param>
        public RequestJson(string url) 
            : base("JsonRequest")
        {
            Url = url;
        }
        
        /// <summary>
        /// Gets or sets the target url
        /// </summary>
        public string Url { get; set; }
    }

    /// <summary>
    /// Request and deserialize JSON side effect command
    /// </summary>
    /// <typeparam name="T">Deserialized type</typeparam>
    public class RequestJson<T> : RequestJson
        where T : IJsonResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestJson{T}"/> class.
        /// </summary>
        /// <param name="url">Target url</param>
        public RequestJson(string url) 
            : base(url)
        {
        }
    }
}