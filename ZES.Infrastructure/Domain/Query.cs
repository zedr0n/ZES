using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class Query<T> : IQuery<T>
    {
        /// <summary>
        /// Gets query type 
        /// </summary>
        /// <value>
        /// Query type 
        /// </value>
        public string QueryType => GetType().Name;
    }
}