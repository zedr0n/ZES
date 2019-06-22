using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class StatsQueryHandler : QueryHandlerBase<StatsQuery, Stats, ValueState<Stats>>
    {
        public StatsQueryHandler(IProjection<ValueState<Stats>> projection)
            : base(projection)
        {
        }

        protected override Stats Handle(IProjection<ValueState<Stats>> projection, StatsQuery query) => 
            projection?.State.Value;
    }
}