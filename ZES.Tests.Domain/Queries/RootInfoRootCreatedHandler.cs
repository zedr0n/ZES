using ZES.Infrastructure.Projections;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoRootCreatedHandler : ProjectionHandlerBase<RootInfoProjectionResults, RootCreated>
    {
        public override RootInfoProjectionResults Handle(RootCreated e, RootInfoProjectionResults state)
        {
            state.SetCreatedAt(e.RootId, e.Timestamp);
            return state;
        }
    }
}