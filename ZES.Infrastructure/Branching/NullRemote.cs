using System.Threading.Tasks;
using SqlStreamStore;
using ZES.Infrastructure.Attributes;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Branching
{
    /// <inheritdoc />
    public class NullRemote<T> : IRemote<T> 
        where T : IEventSourced
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullRemote{T}"/> class.
        /// </summary>
        /// <param name="localStore">Local stream store</param>
        /// <param name="remoteStore">Target remote</param>
        public NullRemote(IStreamStore localStore, [Remote] IStreamStore remoteStore)
        {
        }

        /// <inheritdoc />
        public Task<PushResult> Push(string branchId) => Task.FromResult(new PushResult()); 

        /// <inheritdoc />
        public Task Pull(string branchId) => Task.CompletedTask;
    }
}