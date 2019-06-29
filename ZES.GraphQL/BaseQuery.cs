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
        /// GraphQL error query type
        /// </summary>
        /// <returns><see cref="IError"/></returns>
        public abstract Error Error();

        /// <summary>
        /// GraphQL log query
        /// </summary>
        /// <returns>Log contents</returns>
        public abstract string Log();

        /// <summary>
        /// Get commands pertaining to the stream
        /// </summary>
        /// <param name="key">Full stream key</param>
        /// <returns>Set of mutations</returns>
        public abstract string GetCommands(string key);
    }
}