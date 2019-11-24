using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Causality
{
    /// <summary>
    /// Read-only graph interface
    /// </summary>
    public interface IReadGraph
    {
        /// <summary>
        /// Gets reading state
        /// </summary>
        IObservable<GraphReadState> State { get; }
        
        /// <summary>
        /// Start processing read queries
        /// </summary>
        void Start();

        /// <summary>
        /// Graph size should match the total event store sizes
        /// </summary>
        /// <returns>Graph size</returns>
        Task<long> Size();

        /// <summary>
        /// Pause processing reads 
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
        Task Pause();

        /// <summary>
        /// Stream version query
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<int> GetStreamVersion(string key);

        /// <summary>
        /// Export graph to json
        /// </summary>
        /// <param name="path">JSON string</param>
        void Export(string path);
    }
}