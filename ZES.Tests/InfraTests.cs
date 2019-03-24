using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.TestDomain;

namespace ZES.Tests
{
    public class InfraTests : Test
    {
        private const int Wait = 30;
        
        [Fact]
        public async void CanSaveRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            await bus.CommandAsync(command);
            
            Observable.Timer(TimeSpan.FromMilliseconds(Wait)).Wait();
            
            var root = await repository.Find<Root>("Root");
            Assert.Equal("Root",root.Id);
        }

        [Fact]
        public async void BusCanBeBusy()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            var command2 = new CreateRootCommand {AggregateId = "Root2"};
            Assert.True(await bus.CommandAsync(command));
            Assert.True(await bus.CommandAsync(command2));
            
            Assert.True(bus.Status == BusStatus.Busy); 
        }

        [Fact]
        public async void CanSaveMultipleRoots()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRootCommand {AggregateId = "Root1"};
            await bus.CommandAsync(command);  
            
            var command2 = new CreateRootCommand {AggregateId = "Root2"};
            await bus.CommandAsync(command2);
            
            Observable.Timer(TimeSpan.FromMilliseconds(Wait)).Wait();

            var root = await repository.Find<Root>("Root1");
            var root2 = await repository.Find<Root>("Root2");
            Assert.NotEqual(root.Id, root2.Id);
        }

        private void RegisterProjections(Container c)
        {
            c.Register<RootProjection>(Lifestyle.Singleton);
            c.Register(typeof(IQueryHandler<,>), new[]
            {
                typeof(CreatedAtHandler)
            }, Lifestyle.Singleton);

        }

        [Fact]
        public async void CanProjectRoot()
        {
            var container = CreateContainer(new List<Action<Container>> { RegisterProjections });
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            await bus.CommandAsync(command); 
            
            Observable.Timer(TimeSpan.FromMilliseconds(Wait)).Wait();
            var query = new CreatedAtQuery("Root");
            var createdAt = bus.Query(query);
            Assert.NotEqual(0, createdAt);
        }

        private void RegisterSagas(Container c)
        {
            c.Register<TestSaga>(Lifestyle.Singleton);
            c.Register<TestSagaHandler>(Lifestyle.Singleton);
        }

        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer( new List<Action<Container>>
            {
                RegisterProjections,
                RegisterSagas
            } );
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            await bus.CommandAsync(command);

            Observable.Timer(TimeSpan.FromMilliseconds(Wait)).Wait();
            var query = new CreatedAtQuery("RootNew");
            var createdAt = bus.Query(query);
            Assert.NotEqual(0, createdAt);
        }

        public InfraTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}