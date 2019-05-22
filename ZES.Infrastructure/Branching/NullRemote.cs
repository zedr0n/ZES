using System.Threading.Tasks;
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
        public NullRemote()
        {
        }

        /// <inheritdoc />
        public Task<FastForwardResult> Push(string branchId) => Task.FromResult(new FastForwardResult { ResultStatus = Status.Success }); 

        /// <inheritdoc />
        public Task<FastForwardResult> Pull(string branchId) => Task.FromResult(new FastForwardResult { ResultStatus = Status.Success });
    }
}