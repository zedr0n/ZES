using System.Threading.Tasks;
using ZES.Interfaces.Domain;

#pragma warning disable 1998

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class DefaultQueryHandler<TQuery, TResult, TState> : QueryHandlerBase<TQuery, TResult, TState>
        where TQuery : class, IQuery<TResult> 
        where TResult : class
        where TState : class, IState, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultQueryHandler{TQuery, TResult, TState}"/> class.
        /// </summary>
        /// <param name="manager">Projection manager</param>
        public DefaultQueryHandler(IProjectionManager manager)
            : base(manager)
        {
        }

        /// <inheritdoc />
        protected override async Task<TResult> Handle(IProjection<TState> projection, TQuery query)
            => projection?.State as TResult;
    }

    /// <inheritdoc />
    public class DefaultQueryHandler<TQuery, TResult> : DefaultQueryHandler<TQuery, TResult, TResult>
        where TQuery : class, IQuery<TResult> 
        where TResult : class, IState, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultQueryHandler{TQuery,TResult}"/> class.
        /// </summary>
        /// <param name="manager">Projection manager</param>
        public DefaultQueryHandler(IProjectionManager manager)
            : base(manager)
        {
        }
    }
}