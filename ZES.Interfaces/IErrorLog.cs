using System;

namespace ZES.Interfaces
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
        /// Process the exception into the error log
        /// </summary>
        /// <param name="error">Exception caught during execution</param>
        void Add(Exception error);
    }
}