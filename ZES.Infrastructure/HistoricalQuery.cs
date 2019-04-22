using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public class HistoricalQuery<TQuery, TResult> : IHistoricalQuery<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public HistoricalQuery(TQuery query, long timestamp)
        {
            Query = query;
            Timestamp = timestamp;
        }

        public long Timestamp { get; }
        public TQuery Query { get; }
    }
}