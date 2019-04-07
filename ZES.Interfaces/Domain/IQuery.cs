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
    public interface IHistoricalQuery<out TQuery, TResult> : IQuery<TResult>, IHistoricalQuery
                                                where TQuery:IQuery<TResult>
    {
        TQuery Query { get; }
 
    }
}