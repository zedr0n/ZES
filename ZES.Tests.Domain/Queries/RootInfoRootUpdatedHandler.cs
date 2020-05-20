using ZES.Infrastructure.Projections;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoRootUpdatedHandler : ProjectionHandlerBase<RootInfoProjectionResults, RootUpdated>
    {
        public override RootInfoProjectionResults Handle(RootUpdated e, RootInfoProjectionResults state)
        {
            state.SetUpdatedAt(e.RootId, e.Timestamp);
            return state;
        }
    }
}