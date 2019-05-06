using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Projections
{
    /// <summary>
    /// Historical projection
    /// </summary>
    public interface IHistoricalProjection : IProjection
    {
        /// <summary>
        /// Initialize the historical projection up to timestamp
        /// </summary>
        /// <param name="timestamp">Point in time</param>
        /// <returns>Task representing asynchronous rebuild of the projection up to the specified point in time</returns>
        Task Init(long timestamp);
    }
}