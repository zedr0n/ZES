using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRoot : Command, ICreateCommand   
    {
        public CreateRoot() { }
        public CreateRoot(string target) 
            : base(target) { }
    }
}
