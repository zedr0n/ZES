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
        
        public double RecordValue { get; private set; } 
        
        protected override bool IdempotentImpl { get; set; } = true;
    }
}