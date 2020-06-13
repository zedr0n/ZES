using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class DefaultSingleQueryHandler<TQuery, TResult> : DefaultQueryHandler<TQuery, TResult>
        where TQuery : class, ISingleQuery<TResult> 
        where TResult : class, ISingleState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSingleQueryHandler{TQuery,TResult}"/> class.
        /// </summary>
        /// <param name="projection">Projection</param>
        public DefaultSingleQueryHandler(IProjection<TResult> projection)
            : base(projection)
        {
        }

        /// <inheritdoc />
        protected override async Task<TResult> HandleAsync(TQuery query)
        {
            Projection.Predicate = s => s.Id == query.Id;
            return await base.HandleAsync(query);
        }
    }
}