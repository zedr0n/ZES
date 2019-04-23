using ZES.Interfaces;
using static ZES.Infrastructure.ErrorLog;

namespace ZES.GraphQL
{
    /// <summary>
    /// Non-domain specific GraphQL root query
    /// </summary>
    public abstract class BaseQuery
    {
        /// <summary>
        ///  GraphQL error query type
        /// </summary>
        /// <returns><see cref="IError"/></returns>
        public abstract Error Error();
    }
}