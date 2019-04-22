using static ZES.Infrastructure.ErrorLog;

namespace ZES.GraphQL
{
    public abstract class BaseQuery
    {
        public abstract Error Error();
    }
}