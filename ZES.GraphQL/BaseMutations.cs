using ZES.Infrastructure;
using ZES.Infrastructure.GraphQl;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class BaseMutations : GraphQlMutation
    {
        private readonly IBranchManager _manager;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseMutations"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        /// <param name="log">Log service</param>
        /// <param name="manager">Branch manager</param>
        public BaseMutations(IBus bus, ILog log, IBranchManager manager) 
            : base(bus, log)
        {
            _manager = manager;
        }

        /// <summary>
        /// Branch mutation
        /// </summary>
        /// <param name="branch">Branch id</param>
        /// <returns>Branch result</returns>
        public bool Branch(string branch)
        {
            if (branch == string.Empty)
                _manager.Reset();
            else
                _manager.Branch(branch).Wait();

            return true;
        }
    }
}