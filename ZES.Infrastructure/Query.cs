using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public class Query<T> : IQuery<T>
    {
        public string QueryType => GetType().Name;
    }
}