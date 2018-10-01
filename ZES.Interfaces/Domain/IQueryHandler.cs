using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    /// <typeparam name="TQuery">Query type</typeparam>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IQueryHandler<in TQuery, TResult> //where TQuery : IQuery<TResult>
    {
        /// <summary>
        /// Query handler processor ( can be overriden via decorators )
        /// </summary>
        /// <param name="query"> Query object </param>
        /// <returns>Query result</returns>
        TResult Handle(TQuery query);
        
        /// <summary>
        /// Query handler processor ( can be overriden via decorators )
        /// </summary>
        /// <param name="query"> Query object </param>
        /// <returns>Query result</returns>
        Task<TResult> HandleAsync(TQuery query);
    }
}