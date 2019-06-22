using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class CreateRootHandler : CreateCommandHandlerBase<CreateRoot, Root>
    {
        public CreateRootHandler(IEsRepository<IAggregate> repository)
            : base(repository) { }

        protected override Root Create(CreateRoot command) => new Root(command.Target);
    }
}