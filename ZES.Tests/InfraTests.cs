using Xunit;
using Xunit.Abstractions;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.TestDomain;

namespace ZES.Tests
{
    public class InfraTests : Test
    {
        [Fact]
        public async void CanSaveRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            await bus.CommandAsync(command);

            var root = await repository.Find<Root>("Root");
            Assert.Equal("Root",root.Id);
        }

        [Fact]
        public async void CanSaveMultipleRoots()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRootCommand {AggregateId = "Root1"};
            await bus.CommandAsync(command);  
            
            var command2 = new CreateRootCommand() {AggregateId = "Root2"};
            await bus.CommandAsync(command2);

            var root = await repository.Find<Root>("Root1");
            var root2 = await repository.Find<Root>("Root2");
            Assert.NotEqual(root.Id, root2.Id);
        }

        [Fact]
        public async void CanProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            await bus.CommandAsync(command); 
            
            var query = new CreatedAtQuery("Root");
            var createdAt = bus.Query(query);
            Assert.NotEqual(0, createdAt);
        }

        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            await bus.CommandAsync(command);  
            
            var query = new CreatedAtQuery("RootNew");
            var createdAt = bus.Query(query);
            Assert.NotEqual(0, createdAt);
        }

        public InfraTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}