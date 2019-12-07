using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRecord : Command, ICreateCommand   
    {
        public CreateRecord() { }
        public CreateRecord(string target) 
            : base(target) { }
    }
} 
