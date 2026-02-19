using JetBrains.Annotations;
using ZES.Interfaces.Domain;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRoot(string target) : Command, ICreateCommand
    {
        public string target { get; set; } = target;
        public override string Target => target;
       
        [UsedImplicitly]
        public CreateRoot() : this(null) {}
    }
}

