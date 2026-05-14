using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZES.Interfaces.Clocks;
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

        /// <inheritdoc />
        public virtual Task<TResult> Handle<TState>(TState state, TQuery query) => Handle(query);

        /// <summary>
        /// Convert synchronous handler to async method
        /// </summary>
        /// <param name="query">Typed query</param>
        /// <returns>Task from synchronous result</returns>
        protected abstract Task<TResult> Handle(TQuery query);
    }
}