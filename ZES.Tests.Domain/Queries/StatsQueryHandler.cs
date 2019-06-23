using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class StatsQueryHandler : QueryHandlerBase<StatsQuery, Stats>
    {
        public StatsQueryHandler(IProjection<Stats> projection)
            : base(projection)
        {
        }

        protected override Stats Handle(IProjection<Stats> projection, StatsQuery query) => 
            projection?.State;
    }
}