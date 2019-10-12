using System.Threading.Tasks;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Tracked instance interface
    /// </summary>
    public interface ITracked
    {
        /// <summary>
        /// Gets the task which completes when all operations on the tracked instance are done
        /// </summary>
        Task Completed { get; }

        /// <summary>
        /// Set completion
        /// </summary>
        void Complete();
    }
}