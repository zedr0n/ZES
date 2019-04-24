using ZES.Infrastructure;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class CreatedAtQueryHandler : QueryHandler<CreatedAtQuery, CreatedAt>
    {
        private IProjection<CreatedAtProjection.StateType> _projection;

        public CreatedAtQueryHandler(IProjection<CreatedAtProjection.StateType> projection)
        {
            _projection = projection;
        }

        public override IProjection Projection
        {
            get => _projection;
            set => _projection = value as IProjection<CreatedAtProjection.StateType>;
        }

        public override CreatedAt Handle(CreatedAtQuery query) =>
            new CreatedAt(query.Id, _projection.State.Get(query.Id));
    }
}