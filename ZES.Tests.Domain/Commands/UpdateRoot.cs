using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class UpdateRoot : Command   
    {
        public UpdateRoot() { }
        public UpdateRoot(string target) 
            : base(target) { }
    }
}