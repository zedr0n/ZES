using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class UpdateRoot : Command
    {
        public UpdateRoot(string target) : base(target)
        {
        }
    }
}