using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecordQueryHandler : QueryHandler<LastRecordQuery, LastRecord>
    {
        private readonly IProjection<ValueState<LastRecord>> _projection;
        public LastRecordQueryHandler(IProjection<ValueState<LastRecord>> projection)
        {
            _projection = projection;
        }

        protected override IProjection Projection => _projection;

        public override LastRecord Handle(IProjection projection, LastRecordQuery query) => 
            (projection as IProjection<ValueState<LastRecord>>)?.State.Value;
    }
}