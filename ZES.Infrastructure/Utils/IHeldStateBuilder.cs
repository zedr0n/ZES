namespace ZES.Infrastructure.Utils
{
    public interface IHeldStateBuilder<TState, TBuilder>
    {
        void InitializeFrom(TState state);
        TState Build();
        TState DefaultState();
    }
}