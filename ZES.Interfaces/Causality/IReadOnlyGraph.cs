using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Causality
{
    /// <summary>
    /// Read-only graph interface
    /// </summary>
    public interface IReadOnlyGraph
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
    }
}