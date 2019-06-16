using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class RecordRoot : Command
    {
        public RecordRoot() { }
        public RecordRoot(string target, double recordValue)
            : base(target)
        {
            RecordValue = recordValue;
        }

        public override bool Idempotent { get; set; } = true;
        public double RecordValue { get; private set; } 
    }
}