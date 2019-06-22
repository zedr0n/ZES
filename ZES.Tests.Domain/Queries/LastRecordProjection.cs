using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecordProjection : Projection<ValueState<LastRecord>>
    {
        public LastRecordProjection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline, Dispatcher.Builder streamDispatcher) 
            : base(eventStore, log, messageQueue, timeline, streamDispatcher)
        {
            Register<RootRecorded>(When);
        }
        
        private static ValueState<LastRecord> When(RootRecorded e, ValueState<LastRecord> state)
        {
            lock (state)
            {
                if (state.Value.TimeStamp < e.Timestamp)
                {
                    state.Value.TimeStamp = e.Timestamp;
                    state.Value.Value = e.RecordValue;
                }
            }
            
            return state;
        }
    }
}