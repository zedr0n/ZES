using System.Linq;
using Xunit;
using Xunit.Abstractions;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;
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

            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            await await bus.CommandAsync(new UpdateRoot("Root"));
            
            var dependents = graph.GetDependents(command.MessageId);
            Assert.Equal(2, dependents.Count());
        }
    }
}