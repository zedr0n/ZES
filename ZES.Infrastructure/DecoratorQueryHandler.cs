using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    public class DecoratorQueryHandler<TQuery, TResult> : QueryHandler<TQuery, TResult> where TQuery : class, IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly ILog _log;
        private readonly ISerializer<IQuery<TResult>> _serializer;

        public DecoratorQueryHandler(IQueryHandler<TQuery, TResult> handler, ILog log, ISerializer<IQuery<TResult>> serializer)
        {
            _handler = handler;
            _log = log;
            _serializer = serializer;
        }

        public override TResult Handle(TQuery query)
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
                _log.Error(e.Message,_handler);
                throw;
            }
        }

        public override async Task<TResult> HandleAsync(TQuery query)
        {
            try
            {
                //if (Projection != null)
                //    _handler.Projection = Projection;
                //else
                //    await _handler.Projection.Complete;
                if(_handler.Projection != null)
                    await _handler.Projection.Complete;
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