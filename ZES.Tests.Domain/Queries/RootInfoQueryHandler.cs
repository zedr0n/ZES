using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoQueryHandler : QueryHandlerBase<RootInfoQuery, RootInfo, RootInfoProjectionResults>
    {
        public RootInfoQueryHandler(IProjection<RootInfoProjectionResults> projection)
            : base(projection)
        {
        }

        protected override RootInfo Handle(IProjection<RootInfoProjectionResults> projection, RootInfoQuery query) =>
            new RootInfo(query.Id, projection.State.CreatedAt(query.Id), projection.State.UpdatedAt(query.Id), projection.State.NumberOfUpdates);
    }
}