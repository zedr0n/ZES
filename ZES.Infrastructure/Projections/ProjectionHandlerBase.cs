using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Projections
{
    public abstract class ProjectionHandlerBase<TState, TEvent> : IProjectionHandler<TState, TEvent> 
        where TEvent : class, IEvent
    {
        public TState Handle(IEvent e, TState state) => Handle(e as TEvent, state);

        public abstract TState Handle(TEvent e, TState state);
    }
}