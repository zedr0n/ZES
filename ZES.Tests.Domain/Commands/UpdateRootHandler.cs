using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class UpdateRootHandler : CommandHandlerBase<UpdateRoot, Root>
    {
        public UpdateRootHandler(IEsRepository<IAggregate> repository)
            : base(repository) { }

        protected override void Act(Root root, UpdateRoot command) => root.Update(); 
    }
}