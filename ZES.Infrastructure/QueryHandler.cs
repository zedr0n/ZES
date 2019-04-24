using System;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Base query handler
    /// <para> - Bridges <see cref="IQuery{TResult}"/> to typed query and provides async/sync base handlers </para>
    /// </summary>
    /// <typeparam name="TQuery">Underlying query</typeparam>
    /// <typeparam name="TResult">Query result</typeparam>
    public abstract class QueryHandler<TQuery, TResult> : IQueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        /// <inheritdoc />
        public virtual IProjection Projection { get; set; }

        /// <summary>
        /// Unimplemented 
        /// </summary>
        /// <param name="query">CQRS query</param>
        /// <returns>Query result</returns>
        /// <exception cref="NotImplementedException">Base unimplemented method</exception>
        public virtual TResult Handle(TQuery query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Bridge generic <see cref="IQuery{TResult}"/> to explicit query type 
        /// </summary>
        /// <param name="query">Generic query</param>
        /// <returns>Query result</returns>
        public TResult Handle(IQuery<TResult> query)
        {
            return Handle(query as TQuery);
        }

        /// <summary>
        /// Convert synchronous handler to async method
        /// </summary>
        /// <param name="query">Typed query</param>
        /// <returns>Task from synchronous result</returns>
        public virtual Task<TResult> HandleAsync(TQuery query)
        {
            return Task.FromResult(Handle(query));
        }

        /// <summary>
        /// Dynamic redirect to appropriate async handler
        /// </summary>
        /// <param name="query">Query type</param>
        /// <returns>Task representing the asynchronous query processing</returns>
        public async Task<TResult> HandleAsync(IQuery<TResult> query)
        {
            return await HandleAsync(query as TQuery);
        }
    }
}