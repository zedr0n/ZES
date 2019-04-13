using System.Threading.Tasks;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Commands
{
    public class UpdateRootHandler : ICommandHandler<UpdateRoot>
    {
        private readonly IDomainRepository _repository;

        public UpdateRootHandler(IDomainRepository repository)
        {
            _repository = repository;
        }

        public async Task Handle(UpdateRoot command)
        {
            var root = await _repository.Find<Root>(command.Target);
            root.Update();
            await _repository.Save(root);
        }
    }
}