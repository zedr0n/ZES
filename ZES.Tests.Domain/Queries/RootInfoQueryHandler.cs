using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoQueryHandler : QueryHandlerBaseEx<RootInfoQuery, RootInfo, RootInfoProjection.Results>
    {
        public RootInfoQueryHandler(IProjection<RootInfoProjection.Results> projection)
            : base(projection)
        {
        }

        protected override RootInfo Handle(IProjection<RootInfoProjection.Results> projection, RootInfoQuery query) =>
            new RootInfo(query.Id, projection.State.CreatedAt(query.Id), projection.State.UpdatedAt(query.Id), projection.State.NumberOfUpdates);
    }
}