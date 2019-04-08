using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Projections;

namespace ZES.Tests.Domain.Queries
{
    public class StatsQueryHandler : IQueryHandler<StatsQuery, long>
    {
        private IProjection<ValueState<Stats>> _projection;
        public StatsQueryHandler(IProjection<ValueState<Stats>> projection)
        {
            _projection = projection;
        }


        public IProjection Projection
        {
            get => _projection;
            set => _projection = value as IProjection<ValueState<Stats>>;
        }

        public long Handle(StatsQuery query)
        {
            return _projection.State.Value.NumberOfRoots; 
        }

        public Task<long> HandleAsync(StatsQuery query)
        {
            return Task.FromResult(Handle(query));
        }
    }
}