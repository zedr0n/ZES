namespace ZES.Infrastructure.Alerts
{
    /// <summary>
    /// JSON request submission alert
    /// </summary>
    public class JsonRequestSubmitted : Alert
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRequestSubmitted"/> class.
        /// </summary>
        /// <param name="requestorId">Request correlation id</param>
        /// <param name="url">JSON endpoint url</param>
        public JsonRequestSubmitted(string requestorId, string url)
        {
            Url = url;
        }
        
        /// <summary>
        /// Gets the correlation request id
        /// </summary>
        public string RequestorId { get; }

        /// <summary>
        /// Gets the endpoint url
        /// </summary>
        public string Url { get; }
    }
}