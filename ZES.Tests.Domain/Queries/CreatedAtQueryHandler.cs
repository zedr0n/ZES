using System.Threading.Tasks;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Projections;

namespace ZES.Tests.Domain.Queries
{
    public class CreatedAtQueryHandler : QueryHandler<CreatedAtQuery, long>
    {
        private IProjection<RootProjection.StateType> _projection;

        public CreatedAtQueryHandler(IProjection<RootProjection.StateType> projection)
        {
            _projection = projection;
        }

        public override IProjection Projection
        {
            get => _projection;
            set => _projection = value as IProjection<RootProjection.StateType>;
        }

        public override long Handle(CreatedAtQuery query)
        { 
            return _projection.State.Get(query.Id); 
        }

        public override Task<long> HandleAsync(CreatedAtQuery query)
        {
            return Task.FromResult(Handle(query));
        }
    }
}