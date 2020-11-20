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
            return new RootInfo
            {
                CreatedAt = e.Timestamp,
                UpdatedAt = e.Timestamp,
                RootId = e.RootId,
            };
        }
        
        public RootInfo Handle(RootUpdated e, RootInfo state)
        {
            return new RootInfo
            {
                CreatedAt = state.CreatedAt,
                UpdatedAt = e.Timestamp,
                NumberOfUpdates = state.NumberOfUpdates + 1,
            };
        }
    }
}