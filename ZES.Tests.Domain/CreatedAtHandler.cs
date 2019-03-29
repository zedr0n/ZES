using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain
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