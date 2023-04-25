using System.Threading.Tasks;

namespace ZES.Interfaces.Replicas
{
    /// <summary>
    /// Remote interface 
    /// </summary>
    public interface IRemote
    {
        /// <summary>
        /// Push a branch to remote
        /// </summary>
        /// <param name="branchId">Timeline id</param>
        /// <returns>Task representing asynchronous push</returns>
        Task<FastForwardResult> Push(string branchId);
        
        /// <summary>
        /// Pulls a branch from remote
        /// </summary>
        /// <param name="branchId">Timeline id</param>
        /// <returns>Task representing asynchronous pull</returns>
        Task<FastForwardResult> Pull(string branchId);
    }
}