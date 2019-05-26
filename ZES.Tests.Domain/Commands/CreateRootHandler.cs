using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRootHandler : ICommandHandler<CreateRoot>
    {
        private readonly IEsRepository<IAggregate> _repository;
        
        public CreateRootHandler(IEsRepository<IAggregate> repository)
        {
            _repository = repository;
        }
        
        public async Task Handle(CreateRoot command)
        {
            var root = new Root(command.Target);
            await _repository.Save(root, command.MessageId);
        }
    }
}