using System.Collections.Generic;

namespace ZES.Interfaces.Net
{
    /// <summary>
    /// JSON result -> IEvent converter
    /// </summary>
    public interface IJsonHandler { }
    
    /// <summary>
    /// JSON result -> IEvent converter
    /// </summary>
    /// <typeparam name="T">JSON response type</typeparam>
    public interface IJsonHandler<in T> : IJsonHandler
        where T : IJsonResult
    {
        /// <summary>
        /// Convert the JSON response to event
        /// </summary>
        /// <param name="response">JSON response</param>
        /// <returns>Event resulting from JSON response</returns>
        IEnumerable<IEvent> Handle(T response);
    }
}