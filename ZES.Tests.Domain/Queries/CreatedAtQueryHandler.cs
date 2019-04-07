using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Projections;

namespace ZES.Tests.Domain.Queries
{
    public class CreatedAtQueryHandler : IQueryHandler<CreatedAtQuery, long>
    {
        private IProjection<RootProjection.StateType> _projection;

        public CreatedAtQueryHandler(IProjection<RootProjection.StateType> projection)
        {
            _projection = projection;
        }

        public long Handle(CreatedAtQuery query)
        {
            return _projection.State.Get(query.Id); 
        }

        public Task<long> HandleAsync(CreatedAtQuery query)
        {
            throw new System.NotImplementedException();
        }
    }
}