using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class ReplaceRootHandler : CommandHandlerBase<ReplaceRoot, Root>
    {
        public ReplaceRootHandler(IEsRepository<IAggregate> repository)
            : base(repository) { }

        protected override void Act(Root root, ReplaceRoot command) 
        {
            throw new System.NotImplementedException();
        }
    }
}