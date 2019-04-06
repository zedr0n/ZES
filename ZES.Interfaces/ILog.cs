using System;

namespace ZES.Interfaces
{
    public interface ILog
    {
        /// <summary>
        /// Writes the diagnostic message at the <c>Trace</c> level.
        /// </summary>
        /// <param name="message">A <see langword="object" /> to be written.</param>
        void Trace(object message);
        
        /// <summary>
        /// Writes the diagnostic message at the <c>Trace</c> level.
        /// </summary>
        /// <param name="message">A <see langword="object" /> to be written.</param>
        /// <param name="instance"></param>
        void Trace(object message, object instance);

        /// <summary>
        /// Writes the diagnostic message at the <c>Debug</c> level.
        /// </summary>
        /// <param name="message">A <see langword="object" /> to be written.</param>
        void Debug(object message);

        /// <summary>
        /// Writes the diagnostic message at the <c>Error</c> level.
        /// </summary>
        /// <param name="message">A <see langword="object" /> to be written.</param>
        /// <param name="instance"></param>
        void Error(object message, object instance = null);

        /// <summary>
        /// Writes the diagnostic message at the <c>Error</c> level.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message">A <see langword="object" /> to be written.</param>
        void Error(Exception e,string message);
        
        /// <summary>
        /// Writes the diagnostic message at the <c>Fatal</c> level.
        /// </summary>
        /// <param name="message">A <see langword="object" /> to be written.</param>
        /// <param name="instance"></param>
        void Fatal(object message, object instance = null);  
    }
}