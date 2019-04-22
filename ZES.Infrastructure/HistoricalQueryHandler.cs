using System.Threading.Tasks;
using ZES.Infrastructure.Projections;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public class HistoricalQueryHandler<TQuery, TResult, TState> : QueryHandler<HistoricalQuery<TQuery, TResult>, TResult>,
                                                                  IQueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly IProjection<TState> _projection;

        public HistoricalQueryHandler(IQueryHandler<TQuery, TResult> handler, IProjection<TState> projection)
        {
            _handler = handler;
            _projection = projection;
        }

        public override async Task<TResult> HandleAsync(HistoricalQuery<TQuery, TResult> query)
        {
            _handler.Projection = _projection;
            var projection = (IHistoricalProjection)_projection;
            await projection.Init(query.Timestamp);
            return await _handler.HandleAsync(query.Query);
        }
    }
}