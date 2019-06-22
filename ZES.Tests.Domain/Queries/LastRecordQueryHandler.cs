using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecordQueryHandler : QueryHandlerBase<LastRecordQuery, LastRecord, ValueState<LastRecord>>
    {
        public LastRecordQueryHandler(IProjection<ValueState<LastRecord>> projection)
            : base(projection)
        {
        }

        protected override LastRecord Handle(IProjection<ValueState<LastRecord>> projection, LastRecordQuery query) => 
            projection?.State.Value;
    }
}