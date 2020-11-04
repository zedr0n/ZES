using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZES.Interfaces.EventStore
{
    /// <summary>
    /// Stream details cache
    /// </summary>
    public interface IStreamLocator
    {
        /// <summary>
        /// Gets the task indicating whether cold stream population has been completed
        /// </summary>
        Task Ready { get; }

        /// <summary>
        /// Get the stream with the given id
        /// </summary>
        /// <typeparam name="T">Underlying stream type</typeparam>
        /// <param name="id">event sourced instance id</param>
        /// <param name="timeline">timeline id</param>
        /// <returns>Stream with given id</returns>
        Task<IStream> Find<T>(string id, string timeline = "master")
            where T : IEventSourced;

        /// <summary>
        /// Gets the current version of the stream
        /// </summary>
        /// <remarks>
        /// This can happen if loading from parent stream
        /// </remarks>
        /// <param name="stream">Steam</param>
        /// <returns>Current version of the stream</returns>
        Task<IStream> Find(IStream stream);

        /// <summary>
        /// Find the branched stream in specified timeline if any
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="timeline">Branch id</param>
        /// <returns>Stream descriptor in the the specified timeline</returns>
        Task<IStream> FindBranched(IStream stream, string timeline);

        /// <summary>
        /// List all streams of the branch
        /// </summary>
        /// <param name="branchId">Branch id</param>
        /// <returns>List of all streams</returns>
        Task<IEnumerable<IStream>> ListStreams(string branchId);

        /// <summary>
        /// List all streams of the branch
        /// </summary>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <param name="branchId">Branch id</param>
        /// <returns>List of all streams</returns>
        Task<IEnumerable<IStream>> ListStreams<T>(string branchId)
            where T : IEventSourced;

        /// <summary>
        /// Create an empty stream descriptor from the event sourced
        /// </summary>
        /// <param name="es">Event sourced instance</param>
        /// <param name="timeline">Target timeline</param>
        /// <returns>Stream descriptor</returns>
        IStream CreateEmpty(IEventSourced es, string timeline = "");
    }
}