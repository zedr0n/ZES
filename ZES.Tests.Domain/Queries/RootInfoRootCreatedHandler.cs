using ZES.Infrastructure.Projections;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoRootCreatedHandler : ProjectionHandlerBase<RootInfo, RootCreated>
    {
        public override RootInfo Handle(RootCreated e, RootInfo state)
        {
            state.CreatedAt = e.Timestamp;
            state.UpdatedAt = state.CreatedAt;
            state.RootId = e.RootId;
            return state;
        }
    }
}