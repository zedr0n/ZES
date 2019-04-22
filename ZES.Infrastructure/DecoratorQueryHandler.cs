using System;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public class DecoratorQueryHandler<TQuery, TResult> : QueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly IErrorLog _errorLog;

        public DecoratorQueryHandler(IQueryHandler<TQuery, TResult> handler, IErrorLog errorLog)
        {
            _handler = handler;
            _errorLog = errorLog;
        }

        public override async Task<TResult> HandleAsync(TQuery query)
        {
            try
            {
                if (_handler.Projection != null)
                    await _handler.Projection.Complete;
                return await _handler.HandleAsync(query);
            }
            catch (Exception e)
            {
                if (!(e is NotImplementedException))
                    _errorLog.Add(e);
                return Handle(query);
            }
        }
    }
}