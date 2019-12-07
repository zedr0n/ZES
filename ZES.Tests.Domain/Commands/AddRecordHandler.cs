using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class AddRecordHandler : CommandHandlerBase<AddRecord, Record>
    {
        public AddRecordHandler(IEsRepository<IAggregate> repository)
            : base(repository) { }

        protected override void Act(Record record, AddRecord command) => record.Root(command.RecordValue, command.Timestamp); 
    }
}