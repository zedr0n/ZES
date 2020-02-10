using System.Threading.Tasks;

namespace ZES.Interfaces.Causality
{
    /// <summary>
    /// Graph representation of the stream store
    /// </summary>
    public interface IGraph
    {
        /// <summary>
        /// Wait for graph to be ready
        /// </summary>
        /// <returns>Completes when subscription for the graph catches up to store</returns>
        Task Wait();
        
        /// <summary>
        /// Repopulate the graph 
        /// </summary>
        /// <returns>Task representing the asynchronous population of the graph</returns>
        Task Populate();

        /// <summary>
        /// Serialise the graph to GraphML
        /// </summary>
        /// <param name="filename">GraphML output filename</param>
        /// <returns>Completes when subscription catches up with store and serialisation is done</returns>
        Task Serialise(string filename = "streams.graphml");

        /// <summary>
        /// Get timestamp of event in stream
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <param name="version">Event version</param>
        /// <returns>Event timestamp</returns>
        long GetTimestamp(string key, int version);

        /// <summary>
        /// Remove stream from graph
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <returns>Completes when the stream is deleted</returns>
        Task DeleteStream(string key);

        /// <summary>
        /// Trim stream after version
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <param name="version">Last version to keep</param>
        /// <returns>Completes when the stream is trimmed</returns>
        Task TrimStream(string key, int version);
    }
}