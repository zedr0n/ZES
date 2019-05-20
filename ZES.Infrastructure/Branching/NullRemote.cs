using System.Threading.Tasks;
using SqlStreamStore;
using ZES.Infrastructure.Attributes;
using ZES.Interfaces;
using static ZES.Interfaces.FastForwardResult;

namespace ZES.Infrastructure.Branching
{
    /// <inheritdoc />
    public class NullRemote : IRemote
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullRemote"/> class.
        /// </summary>
        /// <param name="localStore">Local stream store</param>
        /// <param name="remoteStore">Target remote</param>
        public NullRemote(IStreamStore localStore, [Remote] IStreamStore remoteStore)
        {
        }

        /// <inheritdoc />
        public Task<FastForwardResult> Push(string branchId) => Task.FromResult(new FastForwardResult { ResultStatus = Status.Success }); 

        /// <inheritdoc />
        public Task<FastForwardResult> Pull(string branchId) => Task.FromResult(new FastForwardResult { ResultStatus = Status.Success });
    }
}