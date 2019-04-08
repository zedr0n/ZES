using System.Threading.Tasks;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Projections;

namespace ZES.Tests.Domain.Queries
{
    public class StatsQueryHandler : QueryHandler<StatsQuery, long>
    {
        private IProjection<ValueState<Stats>> _projection;
        public StatsQueryHandler(IProjection<ValueState<Stats>> projection)
        {
            _projection = projection;
        }

        public override IProjection Projection
        {
            get => _projection;
            set => _projection = value as IProjection<ValueState<Stats>>;
        }

        public override long Handle(StatsQuery query)
        {
            return _projection.State.Value.NumberOfRoots; 
        }

        public override Task<long> HandleAsync(StatsQuery query)
        {
            return Task.FromResult(Handle(query));
        }
    }
}