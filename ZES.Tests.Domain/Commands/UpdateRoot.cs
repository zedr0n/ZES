using JetBrains.Annotations;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class UpdateRoot(string target) : Command
    {
        public string RootId { get; set; } = target;
        public override string Target => RootId;
        
        [UsedImplicitly]
        public UpdateRoot() : this(null) {}
    }
}

