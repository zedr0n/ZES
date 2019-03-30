using System.Threading.Tasks;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Projections;

namespace ZES.Tests.Domain.Queries
{
    public class CreatedAtHandler : IQueryHandler<CreatedAtQuery, long>
    {
        private readonly RootProjection _projection;

        public CreatedAtHandler(RootProjection projection)
        {
            _projection = projection;
        }

        public long Handle(CreatedAtQuery query)
        {
            return _projection.Get(query.id); 
        }

        public async Task<long> HandleAsync(CreatedAtQuery query)
        {
            return await Task.FromResult(Handle(query));
        }
    }
}