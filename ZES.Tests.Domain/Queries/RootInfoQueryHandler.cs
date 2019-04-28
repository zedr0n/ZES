using ZES.Infrastructure;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoQueryHandler : QueryHandler<RootInfoQuery, RootInfo>
    {
        private IProjection<RootInfoProjection.StateType> _projection;

        public RootInfoQueryHandler(IProjection<RootInfoProjection.StateType> projection)
        {
            _projection = projection;
        }

        public override IProjection Projection
        {
            get => _projection;
            set => _projection = value as IProjection<RootInfoProjection.StateType>;
        }

        public override RootInfo Handle(RootInfoQuery query) =>
            new RootInfo(query.Id, _projection.State.CreatedAt(query.Id), _projection.State.UpdatedAt(query.Id));
    }
}