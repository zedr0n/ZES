using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain
{
    public class StatsHandler : IQueryHandler<StatsQuery, long>
    {
        private readonly StatsProjection _projection;

        public StatsHandler(StatsProjection projection)
        {
            _projection = projection;
        }

        public long Handle(StatsQuery query)
        {
            return _projection.Get(); 
        }

        public async Task<long> HandleAsync(StatsQuery query)
        {
            return await Task.FromResult(Handle(query));
        }
    }
}