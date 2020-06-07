using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class QueryHandlerBase<TQuery, TResult, TState> : QueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        private readonly IProjection<TState> _projection;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryHandlerBase{TQuery,TResult,TState}"/> class.
        /// </summary>
        /// <param name="projection">Projection with State TState</param>
        protected QueryHandlerBase(IProjection<TState> projection)
        {
            _projection = projection;
        }

        /// <inheritdoc />
        protected override IProjection Projection => _projection;
        
        /// <inheritdoc />
        public override TResult Handle(IProjection projection, TQuery query)
        {
            return Handle(projection as IProjection<TState>, query);
        }
        
        /// <summary>
        /// Strongly-typed handler
        /// </summary>
        /// <param name="projection">Project</param>
        /// <param name="query">Query</param>
        /// <returns>Query result</returns>
        protected abstract TResult Handle(IProjection<TState> projection, TQuery query);
    }
}