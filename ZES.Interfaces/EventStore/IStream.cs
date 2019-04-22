namespace ZES.Interfaces.EventStore
{
    public interface IStream
    {
        /// <summary>
        /// Gets unique key identifying the stream
        /// </summary>
        string Key { get; }
        
        /// <summary>
        /// Gets or sets last stream event version
        /// </summary>
        int Version { get; set; }
        
        /// <summary>
        /// Stream timeline id
        /// </summary>
        string Timeline { get; set; }
    }
    
    public interface IStreamLocator<in I>
        where I : IEventSourced
    {
        /// <summary>
        /// Get the stream with the given id
        /// </summary>
        /// <typeparam name="T">Underlying stream type</typeparam>
        /// <param name="id">event sourced instance id</param>
        /// <param name="timeline">timeline id</param>
        /// <returns>Stream with given id</returns>
        IStream Find<T>(string id, string timeline = "master")
            where T : I;
        
        /// <summary>
        /// Extract and cache the stream details for event sourced instance
        /// </summary>
        /// <param name="es">saga or aggregate</param>
        /// <param name="timeline">timeline id</param>
        /// <returns>Stream corresponding to the aggregate root</returns>
        IStream GetOrAdd(I es, string timeline = "master");
        
        /// <summary>
        /// Extract and cache the stream details for the target stream
        /// </summary>
        /// <param name="stream">Target stream</param>
        /// <returns>Cached instance of stream</returns>
        IStream GetOrAdd(IStream stream);
    }
}