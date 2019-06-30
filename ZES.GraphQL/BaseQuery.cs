using System.Linq;
using System.Reactive.Linq;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;
using static ZES.Infrastructure.ErrorLog;

namespace ZES.GraphQL
{
    /// <summary>
    /// Non-domain specific GraphQL root query
    /// </summary>
    public class BaseQuery : GraphQlQuery
    {
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseQuery"/> class.
        /// </summary>
        /// <param name="log">Log service</param>
        /// <param name="bus">Bus service</param>
        public BaseQuery(ILog log, IBus bus) 
            : base(bus)
        {
            _log = log;
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
        /// GraphQL log query
        /// </summary>
        /// <returns>Log contents</returns>
        public string Log()
        {
            _log.Info("Ping!");
            return _log.MemoryLogs.LastOrDefault();
        }
    }
}