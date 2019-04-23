using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Query handler
    /// </summary>
    /// <typeparam name="TQuery">Query type</typeparam>
    /// <typeparam name="TResult">Query result type</typeparam>
    public interface IQueryHandler<in TQuery, TResult> 
        where TQuery : IQuery<TResult>
        where TResult : class
    {
        /// <summary>
        /// Gets or sets the projection used by the query handler
        /// </summary>
        /// <value>
        /// The projection used by the query handler
        /// </value>
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