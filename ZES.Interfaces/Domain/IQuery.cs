using System;

namespace ZES.Interfaces.Domain
{
    public interface IQuery {}
    public interface IQuery<TResult> : IQuery
    {
        Type Type { get; }
    }
    public interface IHistoricalQuery {}

    public interface IHistoricalQuery<TResult> : IQuery<TResult>, IHistoricalQuery
    {
        long Timestamp { get; }
        IQuery<TResult> Query { get; }
    }
}