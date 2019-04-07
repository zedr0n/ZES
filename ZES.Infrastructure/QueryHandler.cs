using System;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    public class QueryHandler<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly ILog _log;
        private readonly ISerializer<IQuery> _serializer;

        public QueryHandler(IQueryHandler<TQuery, TResult> handler, ILog log, ISerializer<IQuery> serializer)
        {
            _handler = handler;
            _log = log;
            _serializer = serializer;
        }

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
            return Handle(query);
        }
    }
}