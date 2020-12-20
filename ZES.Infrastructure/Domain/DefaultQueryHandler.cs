using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class DefaultQueryHandler<TQuery, TResult> : QueryHandlerBase<TQuery, TResult, TResult>
        where TQuery : class, IQuery<TResult> 
        where TResult : class, IState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultQueryHandler{TQuery,TResult}"/> class.
        /// </summary>
        /// <param name="manager">Projection manager</param>
        public DefaultQueryHandler(IProjectionManager manager)
            : base(manager)
        {
        }

        /// <inheritdoc />
        protected async override Task<TResult> Handle(IProjection<TResult> projection, TQuery query)
            => projection?.State;
    }
}