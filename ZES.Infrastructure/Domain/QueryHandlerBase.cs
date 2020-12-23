using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class QueryHandlerBase<TQuery, TResult, TState> : QueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
        where TState : IState, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryHandlerBase{TQuery,TResult,TState}"/> class.
        /// </summary>
        /// <param name="manager">Projection manager</param>
        protected QueryHandlerBase(IProjectionManager manager)
        {
            // Projection = projection;
            Projection = manager.GetProjection<TState>();
            Manager = manager;
        }

        /// <summary>
        /// Gets the projection manager 
        /// </summary>
        protected IProjectionManager Manager { get; }

        /// <inheritdoc />
        public override async Task<TResult> Handle(IProjection projection, TQuery query)
        {
            return await Handle(projection as IProjection<TState>, query);
        }

        /// <inheritdoc />
        protected override Task<TResult> Handle(TQuery query)
        {
            var predicate = Projection?.Predicate;
            if (query.Timeline != string.Empty)
            {
                Projection = Manager.GetProjection<TState>(timeline: query.Timeline);
                Projection.Predicate = predicate;
            }
            else if (query.Timestamp != default)
            {
                var historicalProjection = Manager.GetHistoricalProjection<TState>();
                historicalProjection.Timestamp = query.Timestamp;
                Projection = historicalProjection;
                Projection.Predicate = predicate;
            }
            
            return base.Handle(query);
        }

        /// <summary>
        /// Strongly-typed handler
        /// </summary>
        /// <param name="projection">Project</param>
        /// <param name="query">Query</param>
        /// <returns>Query result</returns>
        protected abstract Task<TResult> Handle(IProjection<TState> projection, TQuery query);
    }
}