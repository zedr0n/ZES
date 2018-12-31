using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Tests.TestDomain
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

        public Task<long> HandleAsync(CreatedAtQuery query)
        {
            throw new System.NotImplementedException();
        }
    }
}