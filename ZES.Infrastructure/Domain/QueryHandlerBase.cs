using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class QueryHandlerBase<TQuery, TResult, TState> : QueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
        where TState : IState, new()
    {
        private readonly QueryFlow _queryFlow;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryHandlerBase{TQuery,TResult,TState}"/> class.
        /// </summary>
        /// <param name="manager">Projection manager</param>
        protected QueryHandlerBase(IProjectionManager manager)
        {
            Projection = manager.GetProjection<TState>();
            Manager = manager;
            _queryFlow = new QueryFlow(this, Configuration.DataflowOptions);
        }

        /// <summary>
        /// Gets or sets gets the projection as common base interface
        /// </summary>
        /// <value>
        /// The projection as common base interface
        /// </value>
        protected IProjection Projection { get; set; }

        /// <summary>
        /// Gets the projection manager 
        /// </summary>
        protected IProjectionManager Manager { get; }

        /// <summary>
        /// Gets or sets the projection predicate
        /// </summary>
        protected Func<IStream, bool> Predicate { get; set; } = s => true;
        
        /// <inheritdoc />
        public override async Task<TResult> Handle(IProjection projection, TQuery query)
        {
            return await Handle(projection as IProjection<TState>, query);
        }

        /// <inheritdoc />
        protected override async Task<TResult> Handle(TQuery query)
        {
            var tracked = new TrackedResult<TQuery, TResult>(query);
            await _queryFlow.SendAsync(tracked);
            return await tracked.Task;
        }

        /// <summary>
        /// Strongly-typed handler
        /// </summary>
        /// <param name="projection">Project</param>
        /// <param name="query">Query</param>
        /// <returns>Query result</returns>
        protected abstract Task<TResult> Handle(IProjection<TState> projection, TQuery query);

        private async Task<TResult> HandleEx(TQuery query)
        {
            var projection = Projection;
            if (query.Timeline != string.Empty)
            {
                projection = Manager.GetProjection<TState>(timeline: query.Timeline);
                projection.Predicate = Predicate;
            }
            else if (query.Timestamp != default)
            {
                var historicalProjection = Manager.GetHistoricalProjection<TState>(); 
                historicalProjection.Timestamp = query.Timestamp;
                historicalProjection.Predicate = Predicate;
                projection = historicalProjection;
            }

            await projection.Ready;
            return await Handle(projection, query);
        }
        
        private class QueryFlow : Dataflow<TrackedResult<TQuery, TResult>>
        {
            public QueryFlow(QueryHandlerBase<TQuery, TResult, TState> handler, DataflowOptions dataflowOptions) 
                : base(dataflowOptions)
            {
                var block = new ActionBlock<TrackedResult<TQuery, TResult>>(
                    async q =>
                {
                    var result = await handler.HandleEx(q.Value);
                    q.SetResult(result);
                }, 
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
                RegisterChild(block);
                InputBlock = block;
            }

            public override ITargetBlock<TrackedResult<TQuery, TResult>> InputBlock { get; }
        }
    }
}