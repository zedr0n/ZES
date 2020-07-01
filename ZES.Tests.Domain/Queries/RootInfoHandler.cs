using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoHandler : IProjectionHandler<RootInfo, RootCreated>, IProjectionHandler<RootInfo, RootUpdated>
    {
        public RootInfo Handle(IEvent e, RootInfo state) => Handle((dynamic)e, state);
        
        public RootInfo Handle(RootCreated e, RootInfo state)
        {
            state.CreatedAt = e.Timestamp;
            state.UpdatedAt = state.CreatedAt;
            state.RootId = e.RootId;
            return state;
        }
        
        public RootInfo Handle(RootUpdated e, RootInfo state)
        {
            state.NumberOfUpdates++;
            state.UpdatedAt = e.Timestamp;
            return state;
        }
    }
}