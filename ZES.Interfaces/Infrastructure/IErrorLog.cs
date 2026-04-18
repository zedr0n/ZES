using System;
using System.Collections.Generic;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Infrastructure
{
    /// <summary>
    /// Error log
    /// </summary>
    public interface IErrorLog
    {
        /// <summary>
        /// Gets hot observable representing the errors in the application
        /// ( stores the last error occured )
        /// </summary>
        /// <value>
        /// Hot observable representing the errors in the application
        /// </value>
        IObservable<IError> Observable { get; }

        /// <summary>
        /// Gets a collection of past errors that have occurred during the execution
        /// and are logged within the system.
        /// </summary>
        /// <value>
        /// A collection of previously logged errors.
        /// </value>
        IEnumerable<IError> PastErrors { get; }

        /// <summary>
        /// Adds an exception to the error log, optionally associating it with the originating message
        /// and specifying whether to ignore the exception.
        /// </summary>
        /// <param name="error">The exception that occurred during operation.</param>
        /// <param name="originatingMessage">The message that initiated the operation, if available.</param>
        /// <param name="ignore">A boolean value indicating whether to ignore the error</param>
        void Add(Exception error, IMessage originatingMessage = null, bool ignore = false);
    }
}