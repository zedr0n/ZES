using ZES.Infrastructure.Domain;

namespace ZES.Infrastructure.Net
{
    /// <summary>
    /// Request json side effect command
    /// </summary>
    public class RequestJson : Command
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
}