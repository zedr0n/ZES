namespace ZES.Interfaces.Domain
{
    public interface IProjectionHandler<TState>
    {
        TState Handle(IEvent e, TState state);
    }

    public interface IProjectionHandler<TState, TEvent> : IProjectionHandler<TState>
    {
        TState Handle(TEvent e, TState state);
    }
}