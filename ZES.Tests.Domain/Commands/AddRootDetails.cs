using JetBrains.Annotations;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class AddRootDetails(string target, string[] details) : Command
    {
        public string[] Details { get; } = details;
        public string RootId { get; set; } = target;
        public override string Target => RootId;
        
        [UsedImplicitly]
        public AddRootDetails() : this(null, null) {}
    }
}

