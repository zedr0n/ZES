using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
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
        /// <summary>
        /// Unimplemented 
        /// </summary>
        /// <param name="projection">Projection to use</param>
        /// <param name="query">CQRS query</param>
        /// <returns>Query result</returns>
        /// <exception cref="NotImplementedException">Base unimplemented method</exception>
        public virtual Task<TResult> Handle(IProjection projection, TQuery query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Dynamic redirect to appropriate async handler
        /// </summary>
        /// <param name="query">Query type</param>
        /// <returns>Task representing the asynchronous query processing</returns>
        public async Task<object> Handle(IQuery query)
        {
            return await Handle(query as IQuery<TResult>);
        }

        /// <summary>
        /// Dynamic redirect to appropriate async handler
        /// </summary>
        /// <param name="query">Query type</param>
        /// <returns>Task representing the asynchronous query processing</returns>
        public async Task<TResult> Handle(IQuery<TResult> query)
        {
            return await Handle(query as TQuery);
        }

        /// <summary>
        /// Convert synchronous handler to async method
        /// </summary>
        /// <param name="query">Typed query</param>
        /// <returns>Task from synchronous result</returns>
        protected abstract Task<TResult> Handle(TQuery query);
    }
}