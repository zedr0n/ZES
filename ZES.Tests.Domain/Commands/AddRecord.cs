using JetBrains.Annotations;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class AddRecord(string target, double recordValue) : Command
    {
        public double RecordValue { get; set; } = recordValue;
        public string RecordId { get; set; } = target;
        public override string Target => RecordId;
        
        [UsedImplicitly]
        public AddRecord() : this(null, 0.0) {}
    }
}

