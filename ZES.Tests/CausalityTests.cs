using System.Linq;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure.Branching;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;

namespace ZES.Tests
{
    public class CausalityTests : ZesTest
    {
        public CausalityTests(ITestOutputHelper outputHelper) 
            : base(outputHelper)
        {
        }

        [Fact]
        public async void CanCreateGraph()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var graph = container.GetInstance<ICausalityGraph>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            await await bus.CommandAsync(new UpdateRoot("Root"));
            
            var dependents = graph.GetDependents(command.MessageId);
            Assert.Equal(2, dependents.Count());

            var root = await repository.Find<Root>("Root");

            var causes = graph.GetCauses(root, root.Version, BranchManager.Master);
            Assert.Equal(3, causes.Count());
        }
    }
}