using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class QueryHandlerBase<TQuery, TResult> : QueryHandlerBaseEx<TQuery, TResult, TResult>
        where TQuery : class, IQuery<TResult> 
        where TResult : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryHandlerBase{TQuery,TResult}"/> class.
        /// </summary>
        /// <param name="projection">Projection</param>
        protected QueryHandlerBase(IProjection<TResult> projection)
            : base(projection)
        {
        }
    }
}