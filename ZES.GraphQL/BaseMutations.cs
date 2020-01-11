using ZES.Infrastructure;
using ZES.Infrastructure.GraphQl;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class BaseMutations : GraphQlMutation
    {
        private readonly IBranchManager _manager;
        private readonly IQGraph _graph;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseMutations"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        /// <param name="log">Log service</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="graph">Graph</param>
        public BaseMutations(IBus bus, ILog log, IBranchManager manager, IQGraph graph) 
            : base(bus, log)
        {
            _manager = manager;
            _graph = graph;
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

        /// <summary>
        /// Serialise the graph
        /// </summary>
        public bool SerialiseGraph()
        {
            _graph.Serialise("live");
            return true;
        }
    }
}