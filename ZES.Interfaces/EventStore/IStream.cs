using System;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.EventStore
{
    public interface IStream
    {
        /// <summary>
        /// Unique key identifying the stream
        /// </summary>
        string Key { get; }
        /// <summary>
        /// Last stream event version
        /// </summary>
        int Version { get; set; }
        /// <summary>
        /// Stream timeline id
        /// </summary>
        string TimelineId { get; set; }
    }
    
    public interface IStreamLocator<in I> where I : IEventSourced
    {
        /// <summary>
        /// Get the stream with the given id
        /// </summary>
        /// <param name="id">event sourced instance id</param>
        /// <param name="timeline">timeline id</param>
        /// <returns></returns>
        IStream Find<T>(string id, string timeline = "") where T : I;
        // Stream repository        
        /// <summary>
        /// Extract and cache the stream details for event sourced instance
        /// </summary>
        /// <param name="es">saga or aggregate</param>
        /// <param name="timeline">timeline id</param>
        /// <returns></returns>
        IStream GetOrAdd(I es, string timeline = "");
        /// <summary>
        /// Extract and cache the stream details for the target stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        IStream GetOrAdd(IStream stream);

    }
}