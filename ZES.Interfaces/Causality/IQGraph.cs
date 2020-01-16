using System.Threading.Tasks;

namespace ZES.Interfaces.Causality
{
    /// <summary>
    /// Graph representation of the stream store
    /// </summary>
    public interface IQGraph
    {
        /// <summary>
        /// Repopulate the graph 
        /// </summary>
        /// <returns>Task representing the asynchronous population of the graph</returns>
        Task Populate();
        
        /// <summary>
        /// Serialise the graph to GraphML
        /// </summary>
        /// <param name="filename">GraphML output filename</param>
        void Serialise(string filename = "streams.graphml");

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
        void DeleteStream(string key);

        /// <summary>
        /// Trim stream after version
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <param name="version">Last version to keep</param>
        void TrimStream(string key, int version);
    }
}