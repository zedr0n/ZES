using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Base graphQL query
    /// </summary>
    public class GraphQlQuery
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
        protected TResult QueryAsync<TResult>(IQuery<TResult> query)
        {
            return _bus.QueryAsync(query).Result;
        }
    }
}