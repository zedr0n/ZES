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
    }
}