using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public class HistoricalQuery<TResult> : IHistoricalQuery<TResult> 
    {
        public HistoricalQuery(IQuery<TResult> query,long timestamp)
        {
            Query = query;
            Timestamp = timestamp;
        }

        public IQuery<TResult> Query { get; }
        public long Timestamp { get; }
    }
}