using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class AddRecord : Command   
    {
        public AddRecord() { }
        public AddRecord(string target, double recordValue) 
        {
            RecordValue = recordValue;
        }
        
        public double RecordValue { get; private set; } 
     }
}