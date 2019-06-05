using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class RecordRootHandler : ICommandHandler<RecordRoot>
    {
        private readonly IEsRepository<IAggregate> _repository;

        public RecordRootHandler(IEsRepository<IAggregate> repository)
        {
            _repository = repository;
        }

        public async Task Handle(RecordRoot command)
        {
            var record = await _repository.Find<Record>(command.Target);
            record.Root(command.RecordValue);
            await _repository.Save(record, command.MessageId);
        }
    }
}