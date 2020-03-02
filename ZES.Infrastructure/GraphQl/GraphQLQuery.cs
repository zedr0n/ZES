using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.GraphQl
{
    /// <summary>
    /// GraphQL query
    /// </summary>
    public class GraphQlQuery : IGraphQlQuery
    {
        private readonly IBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphQlQuery"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        protected GraphQlQuery(IBus bus)
        {
            _bus = bus;
        }
        
        /// <summary>
        /// Execute the query via bus
        /// </summary>
        /// <param name="query">Query instance</param>
        /// <typeparam name="TResult">Query result type</typeparam>
        /// <returns>Query result</returns>
        protected TResult Resolve<TResult>(IQuery<TResult> query)
        {
            return _bus.QueryAsync(query).Result;
        }
    }
}