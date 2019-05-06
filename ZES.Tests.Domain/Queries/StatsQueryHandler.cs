using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class StatsQueryHandler : QueryHandler<StatsQuery, Stats>
    {
        private readonly IProjection<ValueState<Stats>> _projection;
        public StatsQueryHandler(IProjection<ValueState<Stats>> projection)
        {
            _projection = projection;
        }

        protected override IProjection Projection => _projection;

        public override Stats Handle(IProjection projection, StatsQuery query) => 
            (projection as IProjection<ValueState<Stats>>)?.State.Value;
    }
}