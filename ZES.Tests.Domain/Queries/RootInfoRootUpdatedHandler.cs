using ZES.Infrastructure.Projections;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoRootUpdatedHandler : ProjectionHandlerBase<RootInfo, RootUpdated>
    {
        public override RootInfo Handle(RootUpdated e, RootInfo state)
        {
            state.NumberOfUpdates++;
            state.UpdatedAt = e.Timestamp;
            return state;
        }
    }
}