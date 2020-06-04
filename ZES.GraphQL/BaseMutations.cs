using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.GraphQl;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class BaseMutations : GraphQlMutation
    {
        private readonly IBranchManager _manager;
        private readonly IGraph _graph;
        private readonly IMessageQueue _messageQueue;
        private readonly IRecordLog _recordLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseMutations"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        /// <param name="log">Log service</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="graph">Graph</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="recordLog">GraphQl record log</param>
        public BaseMutations(IBus bus, ILog log, IBranchManager manager, IGraph graph, IMessageQueue messageQueue, IRecordLog recordLog) 
            : base(bus, log)
        {
            _manager = manager;
            _graph = graph;
            _messageQueue = messageQueue;
            _recordLog = recordLog;
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
        /// Force rebuild the projections
        /// </summary>
        /// <returns>True if successful</returns>
        public bool RebuildProjections()
        {
            _messageQueue.Alert(new InvalidateProjections());
            return true;
        }

        /// <summary>
        /// Serialise the graph
        /// </summary>
        /// <returns>True if successful</returns>
        public bool SerialiseGraph()
        {
            _graph.Serialise("live");
            return true;
        }

        /// <summary>
        /// Persist log to file
        /// </summary>
        /// <returns>True if successful</returns>
        public bool FlushLog()
        {
            _recordLog.Flush();
            return true;
        }
    }
}