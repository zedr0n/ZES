using System;
using System.Threading.Tasks;
using ZES.Infrastructure.Projections;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public class HistoricalQueryHandler<TQuery, TResult,TState> : IHistoricalQueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly IProjection<TState> _projection;

        public HistoricalQueryHandler(IQueryHandler<TQuery, TResult> handler, IProjection<TState> projection)
        {
            _handler = handler;
            _projection = projection;
        }

        public IProjection Projection { get; set; }

        public TResult Handle(TQuery query)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> HandleAsync(TQuery query)
        {
            throw new NotImplementedException();
        }

        public async Task<TResult> HandleAsync(IHistoricalQuery<TResult> query)
        {
            _handler.Projection = _projection;
            var projection = _projection as IHistoricalProjection;
            await projection.Init(query.Timestamp);
            return await _handler.HandleAsync(query.Query as TQuery);
        }
    }
}