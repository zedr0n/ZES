using System.Collections.Generic;

namespace ZES.Interfaces.EventStore
{
    /// <summary>
    /// Stream details cache
    /// </summary>
    public interface IStreamLocator
    {
        /// <summary>
        /// Get the stream with the given key
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <returns>Stream with given id</returns>
        IStream Find(string key);

        /// <summary>
        /// Get the stream with the given id
        /// </summary>
        /// <typeparam name="T">Underlying stream type</typeparam>
        /// <param name="id">event sourced instance id</param>
        /// <param name="timeline">timeline id</param>
        /// <returns>Stream with given id</returns>
        IStream Find<T>(string id, string timeline = "master")
            where T : IEventSourced;
        
        /// <summary>
        /// Extract and cache the stream details for event sourced instance
        /// </summary>
        /// <param name="es">saga or aggregate</param>
        /// <param name="timeline">timeline id</param>
        /// <returns>Stream corresponding to the aggregate root</returns>
        IStream GetOrAdd(IEventSourced es, string timeline = "master");
        
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

        /// <summary>
        /// Find the branched stream in specified timeline if any
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="timeline">Branch id</param>
        /// <returns>Stream descriptor in the the specified timeline</returns>
        IStream FindBranched(IStream stream, string timeline);

        /// <summary>
        /// List all streams of the branch
        /// </summary>
        /// <param name="branchId">Branch id</param>
        /// <returns>List of all streams</returns>
        IEnumerable<IStream> ListStreams(string branchId);

        /// <summary>
        /// List all streams of the branch
        /// </summary>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <param name="branchId">Branch id</param>
        /// <returns>List of all streams</returns>
        IEnumerable<IStream> ListStreams<T>(string branchId)
            where T : IEventSourced;
    }
}