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

        public Task<long> HandleAsync(StatsQuery query)
        {
            throw new System.NotImplementedException();
        }
    }
}