using System.Collections.Generic;
using NodaTime;
using ZES.Interfaces.Clocks;

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
        /// Gets or sets the last snapshot timestamp
        /// </summary>
        Time SnapshotTimestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the last snapshot version
        /// </summary>
        int SnapshotVersion { get; set; }
        
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
        /// Gets the number of deleted messages in stream
        /// </summary>
        int DeletedCount { get; }

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
        /// Number of events actually present in the stream from [start, start+count)
        /// </summary>
        /// <param name="start">First version</param>
        /// <param name="count">Event count</param>
        /// <returns>Number of events</returns>
        int Count(int start, int count = -1);

        /// <summary>
        /// Register the deletion of an event
        /// </summary>
        /// <param name="count">Number of deleted events</param>
        void AddDeleted(int count);
    }
}