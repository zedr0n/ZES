namespace ZES.Interfaces.Domain
{
    public interface ISingleQuery
    {
        string Id { get; }
    }

    public interface ISingleQuery<TResult> : ISingleQuery
        where TResult : ISingleState
    {
    }
}