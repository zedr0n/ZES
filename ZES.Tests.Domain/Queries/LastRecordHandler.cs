using ZES.Infrastructure.Projections;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecordHandler : ProjectionHandlerBase<LastRecord, RootRecorded>
    {
        public override LastRecord Handle(RootRecorded e, LastRecord state)
        {
            lock (state)
            {
                if (state.TimeStamp < e.Timestamp)
                {
                    state.TimeStamp = e.Timestamp;
                    state.Value = e.RecordValue;
                }
            }
            
            return state;
        }
    }
}