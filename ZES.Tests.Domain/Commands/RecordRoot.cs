using ZES.Infrastructure.Attributes;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    [Idempotent]
    public class RecordRoot : Command
    {
        public RecordRoot() { }
        public RecordRoot(string target, double recordValue)
            : base(target)
        {
            RecordValue = recordValue;
        }
        
        public double RecordValue { get; private set; } 
    }
}