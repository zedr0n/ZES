using System;
using System.Linq;
using System.Threading.Tasks;
using SimpleInjector;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Serialization;

namespace ZES
{
    public class QueryHandler<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : class, IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly ILog _log;
        private readonly ISerializer<IQuery> _serializer;
        private readonly Container _container;

        public QueryHandler(IQueryHandler<TQuery, TResult> handler, ILog log, ISerializer<IQuery> serializer, Container container)
        {
            _handler = handler;
            _log = log;
            _serializer = serializer;
            _container = container;
        }

        public TResult HandleHistorical(IHistoricalQuery<TQuery, TResult> query)
        {
            var timestamp = query.Timestamp;
            var prop = _handler.GetType().GetProperty("Projection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var field = _handler.GetType().GetField("_projection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if(prop == null || field == null)
                throw new InvalidOperationException();
            
            var projection = _container.GetHistorical(prop.PropertyType);
            projection.Init(timestamp);
            
            field.SetValue(_handler,projection);
            return _handler.Handle(query.Query);
        }

        public TResult Handle(IQuery<TResult> query)
        {
            if (query.GetType().GetInterfaces().Contains(typeof(IHistoricalQuery)))
                return HandleHistorical(query as IHistoricalQuery<TQuery,TResult>);
            return Handle(query as TQuery);
        }

        public TResult Handle(TQuery query)
        {
            _log.Trace($"{_handler.GetType().Name}.Handle({query.GetType().Name})");
            _log.Debug(_serializer.Serialize(query));
            if (typeof(TQuery).GetInterfaces().Contains(typeof(IHistoricalQuery)))
                return HandleHistorical(query as IHistoricalQuery<TQuery,TResult>);
            
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

        public async Task<TResult> HandleAsync(IQuery<TResult> query)
        {
            if (query.GetType().GetInterfaces().Contains(typeof(IHistoricalQuery)))
                return HandleHistorical(query as IHistoricalQuery<TQuery,TResult>);
            return await HandleAsync(query as TQuery);
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