using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class RecordRootHandler : CommandHandlerBase<RecordRoot, Record>
    {
        public RecordRootHandler(IEsRepository<IAggregate> repository)
            : base(repository) { }

        protected override void Act(Record record, RecordRoot command) => record.Root(command.RecordValue);
    }
}