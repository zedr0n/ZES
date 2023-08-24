using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Events
{
    public class RootRecorded : Event
    {
        public double RecordValue { get; set; }
    }
}