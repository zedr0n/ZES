using System;
using System.Threading.Tasks;
using SimpleInjector;
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
        private readonly Container _container;

        public QueryHandler(IQueryHandler<TQuery, TResult> handler, ILog log, ISerializer<IQuery<TResult>> serializer, Container container)
        {
            _handler = handler;
            _log = log;
            _serializer = serializer;
            _container = container;
        }

        public TResult Handle(IHistoricalQuery<TResult> query)
        {
            var timestamp = query.Timestamp;
            var field = _handler.GetType().GetField("_projection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var projection = _container.GetHistorical(field.FieldType);
            projection.Init(timestamp);
            
            field.SetValue(_handler,projection);
            return _handler.Handle(query.Query as TQuery);
        }
        
        public async Task<TResult> HandleAsync(IHistoricalQuery<TResult> query)
        {
            var timestamp = query.Timestamp;
            var field = _handler.GetType().GetField("_projection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var projection = _container.GetHistorical(field.FieldType);
            await projection.Init(timestamp);
            
            field.SetValue(_handler,projection);
            return _handler.Handle(query.Query as TQuery);
        }


        public IProjection Projection { get; set; }

        public TResult Handle(TQuery query)
        {
            _log.Trace($"{_handler.GetType().Name}.Handle({query.GetType().Name})");
            _log.Debug(_serializer.Serialize(query));
            try
            {
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