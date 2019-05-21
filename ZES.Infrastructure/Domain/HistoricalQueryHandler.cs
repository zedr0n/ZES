using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Projections;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Generic historic query handler 
    /// </summary>
    /// <typeparam name="TQuery">Original query type</typeparam>
    /// <typeparam name="TResult">Query result</typeparam>
    /// <typeparam name="TState">Associated projection state type</typeparam>
    public class HistoricalQueryHandler<TQuery, TResult, TState> : QueryHandler<HistoricalQuery<TQuery, TResult>, TResult>,
                                                                  IQueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly IProjection<TState> _projection;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoricalQueryHandler{TQuery, TResult, TState}"/> class.
        /// </summary>
        /// <param name="handler">Original query handler</param>
        /// <param name="projection">Historical projection injected by DI</param>
        public HistoricalQueryHandler(IQueryHandler<TQuery, TResult> handler, IProjection<TState> projection)
        {
            _handler = handler;
            _projection = projection;
        }

        /// <summary>
        /// Using an injected <see cref="HistoricalProjection{TState}"/> instance resolve the query at specified point in time
        /// </summary>
        /// <param name="query">Historical query</param>
        /// <returns>Task representing the asynchronous execution of historical query</returns>
        protected override async Task<TResult> HandleAsync(HistoricalQuery<TQuery, TResult> query)
        {
            var projection = (IHistoricalProjection)_projection;
            await projection.Init(query.Timestamp);
            await projection.Ready;
            
            return (_handler as QueryHandler<TQuery, TResult>)?.Handle(projection, query.Query);
        }
    }
}