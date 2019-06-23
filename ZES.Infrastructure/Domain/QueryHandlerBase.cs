using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class QueryHandlerBase<TQuery, TResult> : QueryHandlerBaseEx<TQuery, TResult, TResult>
        where TQuery : class, IQuery<TResult> 
        where TResult : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryHandlerBase{TQuery,TResult}"/> class.
        /// </summary>
        /// <param name="projection">Projection</param>
        public QueryHandlerBase(IProjection<TResult> projection)
            : base(projection)
        {
        }

        /// <inheritdoc />
        protected override TResult Handle(IProjection<TResult> projection, TQuery query)
            => projection?.State;
    }
}