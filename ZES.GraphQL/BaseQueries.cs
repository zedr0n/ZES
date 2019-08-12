using System.Linq;
using System.Reactive.Linq;
using ZES.Infrastructure;
using ZES.Infrastructure.GraphQl;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;
using static ZES.Infrastructure.ErrorLog;

namespace ZES.GraphQL
{
    /// <summary>
    /// Non-domain specific GraphQL root query
    /// </summary>
    public class BaseQueries : GraphQlQuery
    {
        private readonly ILog _log;
        private readonly IBranchManager _manager;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseQueries"/> class.
        /// </summary>
        /// <param name="log">Log service</param>
        /// <param name="bus">Bus service</param>
        /// <param name="manager">Branch manager service</param>
        public BaseQueries(ILog log, IBus bus, IBranchManager manager) 
            : base(bus)
        {
            _log = log;
            _manager = manager;
        }

        /// <summary>
        /// GraphQL error query type
        /// </summary>
        /// <returns><see cref="IError"/></returns>
        public Error Error()
        {
            var error = _log.Errors.Observable.FirstAsync().Wait();
            return (Error)error;
        }

        /// <summary>
        /// Active branch property
        /// </summary>
        /// <returns>Active branch</returns>
        public string ActiveBranch() => _manager.ActiveBranch;

        /// <summary>
        /// GraphQL log query
        /// </summary>
        /// <returns>Log contents</returns>
        public string Log() => _log.MemoryLogs.LastOrDefault();
    }
}