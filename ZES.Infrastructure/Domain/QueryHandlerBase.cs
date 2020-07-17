using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class QueryHandlerBase<TQuery, TResult, TState> : QueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
        where TState : IState
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