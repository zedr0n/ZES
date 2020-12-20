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
        /// <param name="manager">Projection manager</param>
        public DefaultSingleQueryHandler(IProjectionManager manager)
            : base(manager)
        {
        }

        /// <inheritdoc />
        protected override async Task<TResult> Handle(TQuery query)
        {
            Projection = Manager.GetProjection<TResult>(query.Id); 
            return await base.Handle(query);
        }
    }
}