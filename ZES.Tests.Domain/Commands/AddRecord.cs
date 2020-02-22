using ZES.Infrastructure.Attributes;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class AddRecord : Command   
    {
        public AddRecord() { }
        public AddRecord(string target, double recordValue) 
            : base(target)
        {
            RecordValue = recordValue;
        }
        
        public double RecordValue { get; private set; } 
     }
}