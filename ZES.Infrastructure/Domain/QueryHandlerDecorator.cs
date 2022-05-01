using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Generic query decorator
    /// ( redirects all exceptions to error log and delays the query until rebuild is complete )
    /// </summary>
    /// <typeparam name="TQuery">Query type to be decorated</typeparam>
    /// <typeparam name="TResult">Query result</typeparam>
    public class QueryHandlerDecorator<TQuery, TResult> : QueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly IErrorLog _errorLog;
        private readonly QueryFlow _queryFlow;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryHandlerDecorator{TQuery,TResult}"/> class.
        /// </summary>
        /// <param name="handler">Query handler to be decorated</param>
        /// <param name="errorLog">Error log</param>
        public QueryHandlerDecorator(IQueryHandler<TQuery, TResult> handler, IErrorLog errorLog)
        {
            _handler = handler;
            _errorLog = errorLog;
            _queryFlow = new QueryFlow(this, Configuration.DataflowOptions);
        }

        /// <summary>
        /// Decorator query handler delaying after the <see cref="IProjection"/> has been rebuilt
        /// <para>
        /// * redirect exceptions to <see cref="IErrorLog"/>
        /// </para>
        /// </summary>
        /// <param name="query">Typed query</param>
        /// <returns>Task representing the result of asynchronous query processing </returns>
        protected override async Task<TResult> Handle(TQuery query)
        {
            var tracked = new TrackedResult<TQuery, TResult>(query);
            await _queryFlow.SendAsync(tracked);
            return await tracked.Task;
        }

        private async Task<TResult> HandleEx(TQuery query)
        {
            try
            {
                return await _handler.Handle(query);
            }
            catch (Exception e)
            {
                _errorLog.Add(e);
                return default(TResult);
            }
        }
        
        private class QueryFlow : Dataflow<TrackedResult<TQuery, TResult>>
        {
            public QueryFlow(QueryHandlerDecorator<TQuery, TResult> handler, DataflowOptions dataflowOptions) 
                : base(dataflowOptions)
            {
                var block = new ActionBlock<TrackedResult<TQuery, TResult>>(
                    async q =>
                    {
                        var result = await handler.HandleEx(q.Value);
                        q.SetResult(result);
                    },
                    Configuration.DataflowOptions.ToDataflowBlockOptions(false, true)); // new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
                RegisterChild(block);
                InputBlock = block;
            }

            public override ITargetBlock<TrackedResult<TQuery, TResult>> InputBlock { get; }
        }
    }
}