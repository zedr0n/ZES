using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RootRecorded : Event
    {
        public RootRecorded(double recordValue)
        {
            RecordValue = recordValue;
        }

        public double RecordValue { get; }
    }
}