using System;
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

        public override async Task<TResult> HandleAsync(TQuery query)
        {
            try
            {
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