using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Projections;

namespace ZES.Tests.Domain.Queries
{
    public class StatsQueryHandler : IQueryHandler<StatsQuery, long>
    {
        private IProjection<ValueState<long>> _projection;

        private StatsProjection Projection
        {
            set => _projection = value;
        }

        public StatsQueryHandler(StatsProjection projection)
        {
            Projection = projection;
        }

        public long Handle(StatsQuery query)
        {
            return _projection.State.Value; 
        }

        public Task<long> HandleAsync(StatsQuery query)
        {
            throw new System.NotImplementedException();
        }
    }
}