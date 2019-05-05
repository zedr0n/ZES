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