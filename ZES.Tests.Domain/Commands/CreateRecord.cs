using JetBrains.Annotations;
using ZES.Interfaces.Domain;
using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRecord(string target) : Command, ICreateCommand
    {
        public string RecordId { get; set; } = target;
        public override string Target => RecordId;
        
        [UsedImplicitly]
        public CreateRecord() : this(null) {}
    }
}

