using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Projections;
using ZES.Tests.Domain.Queries;
using static ZES.ObservableExtensions;

namespace ZES.Tests
{
    public class RebuildTest : ZesTest
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
                var command = new CreateRoot($"Root{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }
            
            var query = new CreatedAtQuery("Root1");
            await RetryUntil(async () => await bus.QueryAsync(query), timeout: TimeSpan.FromSeconds(5));
            var statsQuery = new StatsQuery();
            await bus.QueryAsync(statsQuery);
            
            await messageQueue.Alert(new InvalidateProjections());
            await bus.QueryAsync(statsQuery);
            
            var newCommand = new CreateRoot("OtherRoot");
            await bus.CommandAsync(newCommand);
            var res = await RetryUntil(async () => await bus.QueryAsync(new StatsQuery()), x => x >= numberOfRoots);

            Assert.Equal(numberOfRoots+1 , res);
        }
    }

    public class InfraTests : ZesTest
    {

        [Fact]
        public async void CanSaveRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRoot("Root");
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
            
            var command = new CreateRoot("Root1");
            await bus.CommandAsync(command);  
            
            var command2 = new CreateRoot("Root2");
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
            
            var command = new CreateRoot("Root");
            await bus.CommandAsync(command); 
            
            var query = new CreatedAtQuery("Root");
            var createdAt = await RetryUntil(async () => await bus.QueryAsync(query));
            Assert.NotEqual(0, createdAt);
        }
        
        [Fact]
        public async void CanHistoricalProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await bus.CommandAsync(command); 

            var statsQuery = new StatsQuery();
            await RetryUntil(async () => await bus.QueryAsync(statsQuery) ==1, timeout: TimeSpan.MaxValue);
            //Assert.Equal(1, bus.Query(statsQuery));
            
            var historicalQuery = new HistoricalQuery<StatsQuery,long>(statsQuery, 0);
            var historicalStats = await bus.QueryAsync(historicalQuery);
            Assert.Equal(0, historicalStats);
            
            var liveQuery = new HistoricalQuery<StatsQuery,long>(statsQuery, DateTime.UtcNow.Ticks);
            var liveStats = await bus.QueryAsync(liveQuery);
            Assert.Equal(1, liveStats);
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
                var command = new CreateRoot($"Root{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }
            
            var query = new CreatedAtQuery("Root1");
            var createdAt = await RetryUntil(async () => await bus.QueryAsync(query));
            Assert.NotEqual(0, createdAt); 
            
            var statsQuery = new StatsQuery();
            Assert.Equal(numRoots,await bus.QueryAsync(statsQuery));
        }

        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer(new List<Action<Container>> {Config.RegisterSagas});
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await bus.CommandAsync(command);

            var query = new CreatedAtQuery("RootCopy");
            var createdAt = await RetryUntil(async () => await bus.QueryAsync(query),timeout: TimeSpan.MaxValue);
            
            Assert.NotEqual(0, createdAt);
        }

        public InfraTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}