using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Causality
{
    /// <summary>
    /// Edge type
    /// </summary>
    public enum EdgeType
    {
        STREAM,
        SAGA
    }

    public enum GraphState
    {
        Sleeping,
        Reading,
        Updating
    }
    
    public interface IGraph
    {
        Task Reinitialize();
        IObservable<GraphState> State { get; }
        IObservable<int> ReadRequests { get; }

        /// <summary>
        /// 
        /// </summary>
        void AddEvent(IEvent e);

        Task<int> GetStreamVersion(string key);
        Task Pause(int ms);
    }
}