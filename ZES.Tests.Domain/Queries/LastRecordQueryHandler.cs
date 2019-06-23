using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecordQueryHandler : QueryHandlerBase<LastRecordQuery, LastRecord>
    {
        public LastRecordQueryHandler(IProjection<LastRecord> projection)
            : base(projection)
        {
        }

        protected override LastRecord Handle(IProjection<LastRecord> projection, LastRecordQuery query) => 
            projection?.State;
    }
}