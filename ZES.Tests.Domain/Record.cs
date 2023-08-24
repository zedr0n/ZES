using System.Collections.Generic;
using NodaTime;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Clocks;
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
            When(new RecordCreated { RootId = rootId } );
        }
        
        public Dictionary<Time, double> Values { get; } = new Dictionary<Time, double>();

        public void Root(double recordValue, Time timestamp)
        {
            When(new RootRecorded { RecordValue  = recordValue, Timestamp = timestamp });
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