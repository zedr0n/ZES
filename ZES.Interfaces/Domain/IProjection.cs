namespace ZES.Interfaces.Domain
{
    public interface IProjection {}
    public interface IProjection<TState> : IProjection
    {
        TState State { get; }
    }
}