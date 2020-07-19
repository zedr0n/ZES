using System.Threading;
using System.Threading.Tasks;

namespace ZES.Interfaces.Net
{
    /// <summary>
    /// JSON connector service
    /// </summary>
    public interface IJSonConnector
    {
        /// <summary>
        /// Submit the tracked json request
        /// </summary>
        /// <param name="url">Target url</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Json result task</returns>
        Task<Task<string>> SubmitRequest(string url, CancellationToken token = default);
    }
}