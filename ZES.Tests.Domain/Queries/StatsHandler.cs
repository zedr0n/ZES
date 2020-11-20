using ZES.Infrastructure.Projections;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class StatsHandler : ProjectionHandlerBase<Stats, RootCreated>
    {
        public override Stats Handle(RootCreated e, Stats state)
        {
            return new Stats { NumberOfRoots = state.NumberOfRoots + 1 };
        }
    }
}