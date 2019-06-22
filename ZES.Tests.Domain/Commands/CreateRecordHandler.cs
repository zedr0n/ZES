using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRecordHandler : CreateCommandHandlerBase<CreateRecord, Record>
    {
        public CreateRecordHandler(IEsRepository<IAggregate> repository)
            : base(repository) { }

        protected override Record Create(CreateRecord command) => new Record(command.Target);
    }
}