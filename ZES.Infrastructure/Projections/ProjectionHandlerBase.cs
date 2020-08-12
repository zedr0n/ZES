using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public abstract class ProjectionHandlerBase<TState, TEvent> : IProjectionHandler<TState, TEvent> 
        where TEvent : class, IEvent 
        where TState : IState
    {
        /// <inheritdoc />
        public TState Handle(IEvent e, TState state) => Handle(e as TEvent, state);

        /// <inheritdoc />
        public abstract TState Handle(TEvent e, TState state);
    }
}