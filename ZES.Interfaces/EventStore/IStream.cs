namespace ZES.Interfaces.EventStore
{
    /// <summary>
    /// Object representing the event stream
    /// </summary>
    public interface IStream
    {
        /// <summary>
        /// Gets the underlying stream id
        /// </summary>
        /// <value>
        /// The underlying stream id
        /// </value>
        string Id { get; }
        
        /// <summary>
        /// Gets unique key identifying the stream
        /// </summary>
        /// <value>
        /// Unique key identifying the stream
        /// </value>
        string Key { get; }

        /// <summary>
        /// Gets or sets last stream event version
        /// </summary>
        /// <value>
        /// Last stream event version
        /// </value>
        int Version { get; set; }

        /// <summary>
        /// Gets or sets the parent stream
        /// </summary>
        IStream Parent { get; set; }
        
        /// <summary>
        /// Gets or sets stream timeline id
        /// </summary>
        /// <value>
        /// Stream timeline id
        /// </value>
        string Timeline { get; set; }
        
        /// <summary>
        /// Gets the local position in the stream
        /// </summary>
        /// <param name="expectedVersion">Real event sourced version</param>
        /// <returns>Local position in stream</returns>
        int Position(int? expectedVersion = null); 

        /// <summary>
        /// Create a branch in new timeline
        /// </summary>
        /// <param name="timeline">New timeline</param>
        /// <param name="version">Version to branch from</param>
        /// <returns>Stream descriptor</returns>
        IStream Branch(string timeline, int version);
    }
    
    /// <summary>
    /// Stream details cache
    /// </summary>
    /// <typeparam name="I">Event sourced type( can be aggregate or saga )</typeparam>
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