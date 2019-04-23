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
        long? Timestamp { get; }
    }
}