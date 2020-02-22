using System;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Registry of all event sourced instances
    /// </summary>
    public interface IEsRegistry
    {
        /// <summary>
        /// Get CLR type of the event sourced instance
        /// </summary>
        /// <param name="type">Event sourced instance type</param>
        /// <returns>CLR type</returns>
        Type GetType(string type);
    }
}