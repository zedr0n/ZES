using ZES.Infrastructure.Projections;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class RootCreatedStatsHandler : ProjectionHandlerBase<Stats, RootCreated>
    {
        public override Stats Handle(RootCreated e, Stats state)
        {
            state.Increment();
            return state;
        }
    }
}