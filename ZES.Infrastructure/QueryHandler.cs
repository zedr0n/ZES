using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public abstract class QueryHandler<TQuery,TResult> : IQueryHandler<TQuery,TResult> where TQuery : class, IQuery<TResult>
    {
        public virtual IProjection Projection { get; set; }

        public abstract TResult Handle(TQuery query);
        
        public TResult Handle(IQuery<TResult> query)
        {
            return Handle(query as TQuery);
        }

        public abstract Task<TResult> HandleAsync(TQuery query);

        public async Task<TResult> HandleAsync(IQuery<TResult> query)
        {
            return await HandleAsync(query as TQuery);
        }
    }
}