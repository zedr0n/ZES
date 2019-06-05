using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RootRecorded : Event
    {
        public RootRecorded(string rootId, double recordValue)
        {
            RootId = rootId;
            RecordValue = recordValue;
        }

        public string RootId { get; }
        public double RecordValue { get; }
    }
}