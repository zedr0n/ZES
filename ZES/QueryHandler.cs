using System;
using System.Threading.Tasks;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Serialization;

namespace ZES
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
    public class QueryHandler<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : class, IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly ILog _log;
        private readonly ISerializer<IQuery<TResult>> _serializer;

        public QueryHandler(IQueryHandler<TQuery, TResult> handler, ILog log, ISerializer<IQuery<TResult>> serializer)
        {
            _handler = handler;
            _log = log;
            _serializer = serializer;
        }

        public IProjection Projection { get; set; }

        public TResult Handle(TQuery query)
        {
            _log.Trace($"{_handler.GetType().Name}.Handle({query.GetType().Name})");
            _log.Debug(_serializer.Serialize(query));
            try
            {
                if (Projection != null)
                    _handler.Projection = Projection;
                return _handler.Handle(query);
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
                throw;
            }
        }

        public async Task<TResult> HandleAsync(TQuery query)
        {
            try
            {
                if (Projection != null)
                    _handler.Projection = Projection;
                return await _handler.HandleAsync(query);
            }
            catch (Exception e)
            {
                if(!(e is NotImplementedException))
                    _log.Error(e.Message,_handler);
                return Handle(query); 
            }
        }
    }
}