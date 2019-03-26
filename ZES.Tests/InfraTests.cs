using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.TestDomain;

namespace ZES.Tests
{
    public class BusTests : Test
    {
        public BusTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
        
        [Theory]
        [InlineData(100)]
        public async void BusCanBeBusy(int numberCommands)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            while (numberCommands > 0)
            {
                var command = new CreateRootCommand {AggregateId = $"Root{numberCommands}"};
                numberCommands--;
                Assert.True(await bus.CommandAsync(command));
            }
            Assert.True(bus.Status == BusStatus.Busy); 
        }
    }
    
    public class InfraTests : Test
    {
        private const int Wait = 200;
        
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
            
            var query = new CreatedAtQuery("Root");
            Observable.Interval(TimeSpan.FromMilliseconds(50))
                .TakeUntil(l => bus.Query(query) > 0)
                .Timeout(TimeSpan.FromMilliseconds(1000))
                .Wait();

            var createdAt = bus.Query(query);
            Assert.NotEqual(0, createdAt);
        }

        [Theory]
        [InlineData(1000)]
        public async void CanProjectALotOfRoots(int numberOfRoots)
        {
            var container = CreateContainer(new List<Action<Container>> { RegisterProjections });
            var bus = container.GetInstance<IBus>();

            while (numberOfRoots > 0)
            {
                var command = new CreateRootCommand {AggregateId = $"Root{numberOfRoots}"};
                await bus.CommandAsync(command);
                numberOfRoots--;
            }
            
            var query = new CreatedAtQuery("Root1");
            Observable.Interval(TimeSpan.FromMilliseconds(50))
                .TakeUntil(l => bus.Query(query) > 0)
                .Timeout(TimeSpan.FromMilliseconds(1000))
                .Wait();
            //Observable.Timer(TimeSpan.FromMilliseconds(50))
            //    .DoWhile(() => bus.Query(query) == 0 && DateTime.UtcNow.Subtract(start).Milliseconds < 1000)
            //    .Wait();
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

            var query = new CreatedAtQuery("RootNew");
            Observable.Interval(TimeSpan.FromMilliseconds(50))
                .TakeUntil(l => bus.Query(query) > 0)
                .Timeout(TimeSpan.FromMilliseconds(1000))
                .Wait();

            var createdAt = bus.Query(query);
            Assert.NotEqual(0, createdAt);
        }

        public InfraTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}