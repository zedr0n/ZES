using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecordProjection : SingleProjection<LastRecord>
    {
        public LastRecordProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline) 
            : base(eventStore, log, timeline, messageQueue)
        {
            Register<RootRecorded>(When);
        }
        
        private static LastRecord When(RootRecorded e, LastRecord state)
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