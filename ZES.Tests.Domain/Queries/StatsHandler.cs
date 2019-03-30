using System.Threading.Tasks;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Projections;

namespace ZES.Tests.Domain.Queries
{
    public class StatsHandler : IQueryHandler<Stats, long>
    {
        private readonly StatsProjection _projection;

        public StatsHandler(StatsProjection projection)
        {
            _projection = projection;
        }

        public long Handle(Stats query)
        {
            return _projection.Get(); 
        }

        public async Task<long> HandleAsync(Stats query)
        {
            return await Task.FromResult(Handle(query));
        }
    }
}