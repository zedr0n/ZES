using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    public interface IQueryHandler {}
    /// <typeparam name="TQuery">Query type</typeparam>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IQueryHandler<in TQuery, TResult> : IQueryHandler //where TQuery : IQuery<TResult>
    {
        IProjection Projection { get; set; }
        /// <summary>
        /// Query handler processor ( can be overriden via decorators )
        /// </summary>
        /// <param name="query"> Query object </param>
        /// <returns>Query result</returns>
        TResult Handle(IQuery<TResult> query);
        
        /// <summary>
        /// Query handler processor ( can be overriden via decorators )
        /// </summary>
        /// <param name="query"> Query object </param>
        /// <returns>Query result</returns>
        Task<TResult> HandleAsync(IQuery<TResult> query);
    }
} 