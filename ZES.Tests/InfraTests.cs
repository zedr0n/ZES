using Xunit;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.TestDomain;

namespace ZES.Tests
{
    public class InfraTests : Test
    {
        [Fact]
        public async void CanSaveAggregate()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            await bus.CommandAsync(command);

            var root = await repository.Find<Root>("Root");
            Assert.Equal("Root",root.Id);
        }
    }
}