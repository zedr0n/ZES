using System;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Generic query decorator
    /// ( redirects all exceptions to error log and delays the query until rebuild is complete )
    /// </summary>
    /// <typeparam name="TQuery">Query type to be decorated</typeparam>
    /// <typeparam name="TResult">Query result</typeparam>
    public class DecoratorQueryHandler<TQuery, TResult> : QueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly IErrorLog _errorLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="DecoratorQueryHandler{TQuery, TResult}"/> class.
        /// </summary>
        /// <param name="handler">Query handler to be decorated</param>
        /// <param name="errorLog">Error log</param>
        public DecoratorQueryHandler(IQueryHandler<TQuery, TResult> handler, IErrorLog errorLog)
        {
            _handler = handler;
            _errorLog = errorLog;
        }

        /// <summary>
        /// Decorator query handler delaying after the <see cref="IProjection"/> has been rebuilt
        /// <para>
        /// * redirect exceptions to <see cref="IErrorLog"/>
        /// <para>* uses synchronous handler if asynchronous is not available </para></para>
        /// </summary>
        /// <param name="query">Typed query</param>
        /// <returns>Task representing the result of asynchronous query processing </returns>
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