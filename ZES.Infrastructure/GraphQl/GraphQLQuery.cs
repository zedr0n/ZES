using System;
using System.Reactive.Linq;
using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Infrastructure;

namespace ZES.Infrastructure.GraphQl
{
    /// <summary>
    /// GraphQL query
    /// </summary>
    public class GraphQlQuery : IGraphQlQuery
    {
        private readonly IBus _bus;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphQlQuery"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        /// <param name="log">Log service</param>
        protected GraphQlQuery(IBus bus, ILog log)
        {
            _bus = bus;
            _log = log;
        }
        
        /// <summary>
        /// Execute the query via bus
        /// </summary>
        /// <param name="query">Query instance</param>
        /// <typeparam name="TResult">Query result type</typeparam>
        /// <returns>Query result</returns>
        protected TResult Resolve<TResult>(IQuery<TResult> query)
        {
            var lastError = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();
            
            var result = _bus.QueryAsync(query).Result;
            
            var error = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();
            var isError = error is { Ignore: false } && error != lastError;
            if (isError)
                throw new InvalidOperationException(error.Message);
            return result;
        }
    }
}