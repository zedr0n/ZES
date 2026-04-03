using NodaTime;

namespace ZES.Interfaces
{
    /// <summary>
    /// Independent representation of errors during the execution
    /// </summary>
    public interface IError
    {
        /// <summary>
        /// Gets the error type
        /// </summary>
        /// <value>
        /// Error type
        /// </value>
        string ErrorType { get; }

        /// <summary>
        /// Gets the error message 
        /// </summary>
        /// <value>
        /// The error message 
        /// </value>
        string Message { get; }

        /// <summary>
        /// Gets time at which error occurred 
        /// </summary>
        /// <value>
        /// Error timestamp
        /// </value>
        Instant Timestamp { get; }
        
        /// <summary>
        /// Gets or sets the message that originated the error.
        /// </summary>
        /// <remarks>
        /// This property captures the context of the error by associating it with the corresponding
        /// message that caused the error. It can be useful for tracing, debugging, and logging purposes,
        /// especially in systems where messages are used to drive workflows or events.
        /// </remarks>
        public IMessage OriginatingMessage { get; set; }
    }
}