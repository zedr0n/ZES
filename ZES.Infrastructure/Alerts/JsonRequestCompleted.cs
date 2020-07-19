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
        /// <param name="jsonData">JSON data result</param>
        public JsonRequestCompleted(string jsonData)
        {
            JsonData = jsonData;
        }

        /// <summary>
        /// Gets the json data payload
        /// </summary>
        public string JsonData { get; }
    }
}