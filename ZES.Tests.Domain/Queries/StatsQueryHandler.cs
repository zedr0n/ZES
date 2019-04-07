using System.Threading.Tasks;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Projections;

namespace ZES.Tests.Domain.Queries
{
    public class StatsQueryHandler : IQueryHandler<StatsQuery, long>
    {
        private readonly StatsProjection _projection;

        public StatsQueryHandler(StatsProjection projection)
        {
            _projection = projection;
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