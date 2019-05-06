using System;
using System.Threading.Tasks;
using ZES.Infrastructure;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

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
    
    public class RootInfoQueryHandler : QueryHandler<RootInfoQuery, RootInfo>
    {
        private readonly IProjection<RootInfoProjection.StateType> _projection;

        public RootInfoQueryHandler(IProjection<RootInfoProjection.StateType> projection)
        {
            _projection = projection;
        }

        protected override IProjection Projection => _projection;

        public override RootInfo Handle(IProjection projection, RootInfoQuery query) =>
            Handle(projection as IProjection<RootInfoProjection.StateType>, query);

        private RootInfo Handle(IProjection<RootInfoProjection.StateType> projection, RootInfoQuery query) =>
            new RootInfo(query.Id, projection.State.CreatedAt(query.Id), projection.State.UpdatedAt(query.Id));
    }
}