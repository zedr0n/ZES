using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain
{
    public class CreateRootHandler : CommandHandler<CreateRootCommand,Root>
    {
        public CreateRootHandler(IDomainRepository repository) : base(repository) {}
        protected override Root Act(CreateRootCommand command) => new Root(command.AggregateId);
    }
}