using System.Collections.Generic;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain
{
    public sealed class Record : AggregateRoot, IAggregate
    {
        public Record()
        {
            Register<RecordCreated>(ApplyEvent);
            Register<RootRecorded>(ApplyEvent);
        }
        
        public Record(string rootId)
            : this()
        {
            When(new RecordCreated(rootId));
        }
        
        public Dictionary<long, double> Values { get; } = new Dictionary<long, double>();

        public void Root(double recordValue, long timestamp)
        {
            When(new RootRecorded(recordValue) { Timestamp = timestamp });
        }

        private void ApplyEvent(RecordCreated e)
        {
            Id = e.RootId;
        }

        private void ApplyEvent(RootRecorded e)
        {
            Values.Add(e.Timestamp, e.RecordValue);
        }
    }
}