using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class HistoricalQuery<TQuery, TResult> : IHistoricalQuery<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HistoricalQuery{TQuery, TResult}"/> class.
        /// </summary>
        /// <param name="query">Underlying query</param>
        /// <param name="timestamp">Point in time</param>
        public HistoricalQuery(TQuery query, long timestamp)
        {
            Query = query;
            Timestamp = timestamp;
        }

        /// <inheritdoc />
        public long Timestamp { get; }

        /// <inheritdoc />
        public TQuery Query { get; }

        /// <inheritdoc />
        public string Timeline { get; set; } = string.Empty;
    }
}