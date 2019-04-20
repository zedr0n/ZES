using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using static ZES.ObservableExtensions;

namespace ZES.Tests
{
    public class InfraTests : ZesTest
    {

        [Fact]
        public async void CanSaveRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var root = await repository.Find<Root>("Root");
            Assert.Equal("Root",root.Id);
        }

        [Fact]
        public async void CanSaveMultipleRoots()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRoot("Root1");
            await await bus.CommandAsync(command);  
            
            var command2 = new CreateRoot("Root2");
            await await bus.CommandAsync(command2);

            var root = await repository.Find<Root>("Root1");
            var root2 = await repository.Find<Root>("Root2");

            Assert.NotEqual(root.Id, root2.Id);
        }

        [Fact]
        public async void CanProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command); 
            Thread.Sleep(10);
            
            var query = new CreatedAtQuery("Root");
            //var createdAt = await RetryUntil(async () => await bus.QueryAsync(query), x => x != 0);
            var createdAt = await bus.QueryUntil(query, c => c?.Timestamp != 0 );
            Assert.NotEqual(0, createdAt.Timestamp);
        }
        
        [Fact]
        public async void CanHistoricalProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command); 

            var statsQuery = new StatsQuery();
            //await RetryUntil(async () => (await bus.QueryAsync(statsQuery)).NumberOfRoots == 1, timeout: TimeSpan.FromSeconds(5));
            //Assert.Equal(1, bus.Query(statsQuery));
            
            var historicalQuery = new HistoricalQuery<StatsQuery,Stats>(statsQuery, 0);
            var historicalStats = await bus.QueryAsync(historicalQuery);
            Assert.Equal(0, historicalStats.NumberOfRoots);
            
            var liveQuery = new HistoricalQuery<StatsQuery,Stats>(statsQuery, DateTime.UtcNow.Ticks);
            var liveStats = await bus.QueryAsync(liveQuery);
            Assert.Equal(1, liveStats.NumberOfRoots);
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
            var createdAt = await bus.QueryUntil(query);//await RetryUntil(async () => await bus.QueryAsync(query));
            Assert.NotEqual(0, createdAt.Timestamp); 
            
            var statsQuery = new StatsQuery();
            var stats = await bus.QueryUntil(statsQuery, s => s?.NumberOfRoots == numRoots);
            Assert.Equal(numRoots, stats.NumberOfRoots);
        }

        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer(new List<Action<Container>> {Config.RegisterSagas});
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await bus.CommandAsync(command);

            var query = new CreatedAtQuery("RootCopy");
            var createdAt = await bus.QueryUntil(query, x => x.Timestamp > 0); //await RetryUntil(async () => await bus.QueryAsync(query));
            
            Assert.NotEqual(0, createdAt.Timestamp);
        }

        [Theory]
        [InlineData(10)]
        public async void CanParallelizeSagas(int numRoots)
        {
            var container = CreateContainer(new List<Action<Container>> {Config.RegisterSagas});
            var bus = container.GetInstance<IBus>(); 
            
            var rootId = numRoots; 
            while (rootId > 0)
            {
                var command = new CreateRoot($"Root{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }

            await bus.QueryUntil(new CreatedAtQuery("Root1"));  //await RetryUntil(async () => await bus.QueryAsync(new CreatedAtQuery("Root1")));
            
            rootId = numRoots;
            while (rootId > 0)
            {
                var updateCommand = new UpdateRoot($"Root{rootId}");
                await bus.CommandAsync(updateCommand);
                rootId--;
            }
            
            Thread.Sleep(50);
        }
        
        [Theory]
        [InlineData(50)]         
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
            //await RetryUntil(async () => await bus.QueryAsync(query), timeout: TimeSpan.FromSeconds(5));
            await bus.QueryUntil(query, c => c.Timestamp > 0);
            
            var statsQuery = new StatsQuery();
            await bus.QueryAsync(statsQuery);
            
            messageQueue.Alert(new InvalidateProjections());
            Thread.Sleep(10);
            await bus.QueryAsync(statsQuery);
            
            var newCommand = new CreateRoot("OtherRoot");
            await bus.CommandAsync(newCommand);
            var stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots > numberOfRoots);
            
            Assert.Equal(numberOfRoots+1 , stats?.NumberOfRoots);
        }

        public InfraTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}