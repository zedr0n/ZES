using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using static ZES.ObservableExtensions;

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

    public class RebuildTest : Test
    {
        public RebuildTest(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
        
        [Theory]
        [InlineData(150)]        
        public async void CanRebuildProjection(int numberOfRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var messageQueue = container.GetInstance<IMessageQueue>();

            var rootId = numberOfRoots;
            while (rootId > 0)
            {
                var command = new CreateRootCommand {AggregateId = $"Root{rootId}"};
                await bus.CommandAsync(command);
                rootId--;
            }
            
            var query = new CreatedAtQuery("Root1");
            await RetryUntil(async () => await bus.QueryAsync(query));
            
            await messageQueue.Alert("InvalidProjections");
            Thread.Sleep(10);
            
            var newCommand = new CreateRootCommand {AggregateId = "OtherRoot"};
            await bus.CommandAsync(newCommand);
            var res = await RetryUntil(async () => await bus.QueryAsync(new StatsQuery()), x => x == numberOfRoots + 1);

            Assert.Equal(numberOfRoots + 1, res);
        }
    }
    
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

            var root = await RetryUntil(async () => await repository.Find<Root>("Root"));
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

            var root = await RetryUntil(async () => await repository.Find<Root>("Root1"));
            var root2 = await RetryUntil(async () => await repository.Find<Root>("Root2"));

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
            var createdAt = await RetryUntil(async () => await bus.QueryAsync(query));
            Assert.NotEqual(0, createdAt);
        }

        [Theory]
        [InlineData(10)]
        public async void CanProjectALotOfRoots(int numRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var rootId = numRoots; 
            while (rootId > 0)
            {
                var command = new CreateRootCommand {AggregateId = $"Root{rootId}"};
                await bus.CommandAsync(command);
                rootId--;
            }
            
            var query = new CreatedAtQuery("Root1");
            var createdAt = await RetryUntil(async () => await bus.QueryAsync(query));
            Assert.NotEqual(0, createdAt); 
            
            var statsQuery = new StatsQuery();
            Assert.Equal(numRoots,bus.Query(statsQuery));
        }



        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRootCommand {AggregateId = "Root"};
            await bus.CommandAsync(command);

            var query = new CreatedAtQuery("RootNew");
            var createdAt = await RetryUntil(async () => await bus.QueryAsync(query));
            
            Assert.NotEqual(0, createdAt);
        }

        public InfraTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}