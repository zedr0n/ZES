using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain
{
    public class CreateRootHandler : ICommandHandler<CreateRootCommand>
    {
        private readonly IDomainRepository _repository;
        public CreateRootHandler(IDomainRepository repository)
        {
            _repository = repository;
        }
        public async Task Handle(CreateRootCommand command)
        {
            var root = new Root(command.AggregateId);
            await _repository.Save(root);
        }
    }
}