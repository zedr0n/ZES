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

    public interface IStreamLocator
    {
        /// <summary>
        /// Generate key from aggregate instance
        /// </summary>
        /// <param name="es">Aggregate instance</param>
        /// <returns>aggregate-${key}</returns>
        //string Key(IAggregate es);
        /// <summary>
        /// Generate key from saga instance
        /// </summary>
        /// <param name="es">Saga instance</param>
        /// <returns>saga-${key}</returns>
        //string Key(ISaga es);
        /// <summary>
        /// Get the stream with the given id
        /// </summary>
        /// <param name="id">event sourced instance id</param>
        /// <param name="timeline">timeline id</param>
        /// <returns></returns>
        IStream Find<T>(string id, string timeline = "") where T : IEventSourced;
        // Stream repository        
        /// <summary>
        /// Extract and cache the stream details for event sourced instance
        /// </summary>
        /// <param name="es">saga or aggregate</param>
        /// <param name="timeline">timeline id</param>
        /// <returns></returns>
        IStream GetOrAdd(IEventSourced es, string timeline = "");
        /// <summary>
        /// Extract and cache the stream details for the target stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        IStream GetOrAdd(IStream stream);

    }
}