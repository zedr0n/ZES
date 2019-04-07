namespace ZES.Interfaces.Domain
{
    public interface IQuery {}
    public interface IQuery<TResult> : IQuery
    {
        
    }

    public interface IHistoricalQuery
    {
        long Timestamp { get; }
    }
    public interface IHistoricalQuery<TResult> : IQuery<TResult>, IHistoricalQuery
    {
        IQuery<TResult> Query { get; }
    }
}