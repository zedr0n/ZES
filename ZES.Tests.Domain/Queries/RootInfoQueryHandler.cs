using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    /*public class ProjectionProxy<T> : IProjection<T>
    {
        private readonly Lazy<IProjection<T>> _projection;

        public ProjectionProxy(Lazy<IProjection<T>> projection)
        {
            _projection = projection;
        }

        public Task Complete => _projection.Value.Complete;
        public string Key(IStream stream)
        {
            throw new NotImplementedException();
        }

        public T State => _projection.Value.State;
    }*/
    
    public class RootInfoQueryHandler: QueryHandlerBase<RootInfoQuery, RootInfo, RootInfoProjection.StateType>
    {
        public RootInfoQueryHandler(IProjection<RootInfoProjection.StateType> projection)
            : base(projection)
        {
        }

        protected override RootInfo Handle(IProjection<RootInfoProjection.StateType> projection, RootInfoQuery query) =>
            new RootInfo(query.Id, projection.State.CreatedAt(query.Id), projection.State.UpdatedAt(query.Id));
    }
}