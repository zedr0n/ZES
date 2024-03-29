using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Generic historic query handler 
    /// </summary>
    /// <typeparam name="TQuery">Original query type</typeparam>
    /// <typeparam name="TResult">Query result</typeparam>
    /// <typeparam name="TState">Associated projection state type</typeparam>
    public class HistoricalQueryHandler<TQuery, TResult, TState> : QueryHandlerBase<HistoricalQuery<TQuery, TResult>, TResult, TState>,
                                                                  IQueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
        where TState : IState, new()
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoricalQueryHandler{TQuery, TResult, TState}"/> class.
        /// </summary>
        /// <param name="handler">Original query handler</param>
        /// <param name="manager">Projection manager</param>
        /// <param name="activeTimeline">Active timeline</param>
        public HistoricalQueryHandler(IQueryHandler<TQuery, TResult> handler, IProjectionManager manager, ITimeline activeTimeline)
            : base(manager, activeTimeline)
        {
            _handler = handler;
        }

        /// <summary>
        /// Using an injected <see cref="HistoricalProjection{TState}"/> instance resolve the query at specified point in time
        /// </summary>
        /// <param name="query">Historical query</param>
        /// <returns>Task representing the asynchronous execution of historical query</returns>
        protected override async Task<TResult> Handle(HistoricalQuery<TQuery, TResult> query)
        {
            query.Query.Timestamp = query.Timestamp;
            return await _handler.Handle(query.Query);
        }

        /// <inheritdoc />
        protected override Task<TResult> Handle(IProjection<TState> projection, HistoricalQuery<TQuery, TResult> query)
        {
            throw new System.NotImplementedException();
        }
    }
}