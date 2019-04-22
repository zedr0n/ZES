namespace ZES.Interfaces.Domain
{
    public interface IQuery { }
    public interface IQuery<TResult> : IQuery { }
    public interface IHistoricalQuery { }
    public interface IHistoricalQuery<out TQuery, TResult> : IQuery<TResult>, IHistoricalQuery
    {
        long Timestamp { get; }
        TQuery Query { get; }
    }
}