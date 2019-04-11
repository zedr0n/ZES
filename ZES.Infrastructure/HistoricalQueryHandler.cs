using System.Threading.Tasks;
using ZES.Infrastructure.Projections;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public class HistoricalQueryHandler<TQuery, TResult,TState> : QueryHandler<HistoricalQuery<TQuery,TResult>, TResult>,
                                                                  IQueryHandler<TQuery,TResult> where TQuery : class, IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly IProjection<TState> _projection;

        public HistoricalQueryHandler(IQueryHandler<TQuery, TResult> handler, IProjection<TState> projection)
        {
            _handler = handler;
            _projection = projection;
        }

        public override async Task<TResult> HandleAsync(HistoricalQuery<TQuery,TResult> query)
        {
            _handler.Projection = _projection;
            var projection = _projection as IHistoricalProjection;
            await projection.Init(query.Timestamp);
            return await _handler.HandleAsync(query.Query);
        }
    }
}