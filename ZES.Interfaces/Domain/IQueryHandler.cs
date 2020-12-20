using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Query handlers are used to (asynchronously) provide the results to the client 
    /// </summary>
    public interface IQueryHandler
    {
        /// <summary>
        /// Query handler processor ( can be overriden via decorators )
        /// </summary>
        /// <param name="query"> Query object </param>
        /// <returns>Query result</returns>
        Task<object> Handle(IQuery query);
    }

    /// <summary>
    /// Query handlers are used to (asynchronously) provide the results to the client 
    /// </summary>
    /// <typeparam name="TQuery">Query type</typeparam>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IQueryHandler<in TQuery, TResult> : IQueryHandler 
        where TQuery : IQuery<TResult>
        where TResult : class
    {
        /// <summary>
        /// Query handler processor ( can be overriden via decorators )
        /// </summary>
        /// <param name="query"> Query object </param>
        /// <returns>Query result</returns>
        Task<TResult> Handle(IQuery<TResult> query);
    }
} 