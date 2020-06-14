using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class HistoricalSingleQueryHandler<TQuery, TResult, TState> : HistoricalQueryHandler<TQuery, TResult, TState>
        where TQuery : class, ISingleQuery<TResult>
        where TResult : class, ISingleState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HistoricalSingleQueryHandler{TQuery, TResult, TState}"/> class.
        /// </summary>
        /// <param name="handler">Original query handler</param>
        /// <param name="projection">Historical projection injected by DI</param>
        public HistoricalSingleQueryHandler(IQueryHandler<TQuery, TResult> handler, IProjection<TState> projection)
            : base(handler, projection)
        {
        }

        /// <inheritdoc />
        protected override Task<TResult> HandleAsync(HistoricalQuery<TQuery, TResult> query)
        {
            Projection.Predicate = s => s.Id == query.Query.Id;
            return base.HandleAsync(query);
        }
    }
}