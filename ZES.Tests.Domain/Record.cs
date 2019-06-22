using System.Collections.Generic;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain
{
    public sealed class Record : EventSourced, IAggregate
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

        public void Root(double recordValue)
        {
            When(new RootRecorded(Id, recordValue));
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