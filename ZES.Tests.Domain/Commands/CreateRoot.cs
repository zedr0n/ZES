using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRoot : Command 
    {
        public CreateRoot(string target) : base(target)
        {
        }
    }
}