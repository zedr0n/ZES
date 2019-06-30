namespace ZES.GraphQL
{
    /// <summary>
    /// Log message subscription result
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogMessage"/> class.
        /// </summary>
        /// <param name="message">Message string</param>
        public LogMessage(string message)
        {
            Message = message;
        }

        /// <summary>
        /// Gets or sets the message string
        /// </summary>
        /// <value>
        /// The message string
        /// </value>
        public string Message { get; set; }
    }
}