using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class DefaultQueryHandler<TQuery, TResult> : QueryHandlerBase<TQuery, TResult, TResult>
        where TQuery : class, IQuery<TResult> 
        where TResult : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultQueryHandler{TQuery,TResult}"/> class.
        /// </summary>
        /// <param name="projection">Projection</param>
        public DefaultQueryHandler(IProjection<TResult> projection)
            : base(projection)
        {
        }

        /// <inheritdoc />
        protected override async Task<TResult> HandleAsync(TQuery query)
        {
            if (query is ISingleQuery singleQuery)
                Projection.Predicate = s => s.Id == singleQuery.Id;
            return await base.HandleAsync(query);
        }

        /// <inheritdoc />
        protected override TResult Handle(IProjection<TResult> projection, TQuery query)
            => projection?.State;
    }
}