using System.Collections.Generic;

namespace ZES.Interfaces.EventStore
{
    /// <summary>
    /// Object representing the event stream
    /// </summary>
    public interface IStream
    {
        /// <summary>
        /// Gets a value indicating whether whether stream is a saga stream
        /// </summary>
        /// <value>
        /// True if stream is a saga ( type contains "Saga" ) 
        /// </value>
        bool IsSaga { get; }
        
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
        /// <value>
        /// The parent stream
        /// </value>
        IStream Parent { get; set; }
        
        /// <summary>
        /// Gets the ancestors( set of all parents ) 
        /// </summary>
        /// <value>
        /// Set of all parents 
        /// </value>
        IEnumerable<IStream> Ancestors { get; }
        
        /// <summary>
        /// Gets or sets stream timeline id
        /// </summary>
        /// <value>
        /// Stream timeline id
        /// </value>
        string Timeline { get; set; }

        /// <summary>
        /// Gets aggregate target type
        /// </summary>
        /// <value>
        /// Aggregate target type
        /// </value>
        string Type { get; }

        /// <summary>
        /// Copies the stream
        /// </summary>
        /// <returns>Stream copy</returns>
        IStream Copy();
        
        /// <summary>
        /// Gets the local position in the stream
        /// </summary>
        /// <param name="expectedVersion">Real event sourced version</param>
        /// <returns>Local position in stream</returns>
        int ReadPosition(int expectedVersion); 

        /// <summary>
        /// Create a branch in new timeline
        /// </summary>
        /// <param name="timeline">New timeline</param>
        /// <param name="version">Version to branch from</param>
        /// <returns>Stream descriptor</returns>
        IStream Branch(string timeline, int version);

        /// <summary>
        /// Append position for the split stream
        /// </summary>
        /// <returns>Append position</returns>
        int AppendPosition();

        /// <summary>
        /// Number of events actually present in the stream from [start, start+count)
        /// </summary>
        /// <param name="start">First version</param>
        /// <param name="count">Event count</param>
        /// <returns>Number of events</returns>
        int Count(int start, int count = -1);
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

        /// <summary>
        /// Gets the current version of the stream
        /// </summary>
        /// <remarks>
        /// This can happen if loading from parent stream
        /// </remarks>
        /// <param name="stream">Steam</param>
        /// <returns>Current version of the stream</returns>
        IStream Find(IStream stream);
    }
}