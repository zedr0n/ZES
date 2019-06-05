using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRecordHandler : ICommandHandler<CreateRecord>
    {
        private readonly IEsRepository<IAggregate> _repository;

        public CreateRecordHandler(IEsRepository<IAggregate> repository)
        {
            _repository = repository;
        }

        public async Task Handle(CreateRecord command)
        {
            var record = new Record(command.Target);
            await _repository.Save(record, command.MessageId);
        }
    }
}