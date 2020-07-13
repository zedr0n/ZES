using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZES.Interfaces
{
    /// <summary>
    /// Application logger
    /// </summary>
    public interface ILog
    {
        /// <summary>
        /// Gets the stopwatch 
        /// </summary>
        IStopWatch StopWatch { get; }
        
        /// <summary>
        /// Gets or sets gets error log
        /// </summary>
        /// <value>
        /// Error log
        /// </value>
        IErrorLog Errors { get; set; }

        /// <summary>
        /// Gets all accumulated logs in memory
        /// </summary>
        /// <value>
        /// All accumulated logs in memory
        /// </value>
        IList<string> MemoryLogs { get; }

        /// <summary>
        /// Gets logs as observable
        /// </summary>
        /// <value>
        /// Logs as observable
        /// </value>
        IObservable<string> Logs { get; }

        /// <summary>
        /// Writes the diagnostic message at the <c>Trace</c> level.
        /// </summary>
        /// <param name="message">Log message</param>
        void Trace(object message);
        
        /// <summary>
        /// Writes the diagnostic message at the <c>Trace</c> level.
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="instance">Object originating the trace</param>
        void Trace(object message, object instance);

        /// <summary>
        /// Writes the diagnostic message at the <c>Debug</c> level.
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="instance">Object originating the debug message</param>
        /// <param name="caller">Compiler caller name</param>
        void Debug(object message, object instance = null, [CallerMemberName] string caller = "");

        /// <summary>
        /// Writes the diagnostic message at the <c>Info</c> level.
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="instance">Object originating the error</param>
        void Info(object message, object instance = null);
        
        /// <summary>
        /// Writes the diagnostic message at the <c>Error</c> level.
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="instance">Object originating the error</param>
        void Error(object message, object instance = null);

        /// <summary>
        /// Writes the diagnostic message at the <c>Fatal</c> level.
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="instance">Object originating the error</param>
        void Fatal(object message, object instance = null);

        /// <summary>
        /// Writes the diagnostic message at the <c>Warn</c> level.
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="instance">Object originating the error</param>
        void Warn(object message, object instance = null);
    }
}