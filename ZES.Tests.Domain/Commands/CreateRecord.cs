using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRecord : Command
    {
        public CreateRecord() { }
        public CreateRecord(string target)
            : base(target)
        { }
    }
}