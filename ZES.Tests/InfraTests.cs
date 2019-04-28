using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;

namespace ZES.Tests
{
    public class InfraTests : ZesTest
    {
        public InfraTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }
        
        [Fact]
        public async void CanSaveRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var root = await repository.Find<Root>("Root");
            Assert.Equal("Root", root.Id);
        }

        [Fact]
        public async void CannotSaveTwice()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var errorLog = container.GetInstance<IErrorLog>();

            IError error = null;
            errorLog.Errors.Subscribe(e => error = e);
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);
            await await bus.CommandAsync(command);
            Assert.Equal(typeof(InvalidOperationException).Name, error.ErrorType); 
            Assert.Contains("ahead", error.Message);
            Assert.NotNull(error.Timestamp);
        }

        [Fact]
        public async void CanSaveMultipleRoots()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            
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
            
            var query = new RootInfoQuery("Root");
            var rootInfo = await bus.QueryUntil(query, c => c?.CreatedAt != 0);
            Assert.NotEqual(0, rootInfo?.CreatedAt);
        }
        
        [Fact]
        public async void CanUpdateRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);
            
            var updateCommand = new UpdateRoot("Root");
            await await bus.CommandAsync(updateCommand);

            var rootInfo = await bus.QueryUntil(new RootInfoQuery("Root"), r => r.UpdatedAt > r.CreatedAt);
            Assert.True(rootInfo.UpdatedAt > rootInfo.CreatedAt);
        }
        
        [Fact]
        public async void CanHistoricalProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command); 

            var statsQuery = new StatsQuery();
            
            var historicalQuery = new HistoricalQuery<StatsQuery, Stats>(statsQuery, 0);
            var historicalStats = await bus.QueryAsync(historicalQuery);
            Assert.Equal(0, historicalStats.NumberOfRoots);
            
            var liveQuery = new HistoricalQuery<StatsQuery, Stats>(statsQuery, DateTime.UtcNow.Ticks);
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

            var query = new RootInfoQuery("Root1");
            var rootInfo = await bus.QueryUntil(query, c => c?.CreatedAt > 0);
            Assert.NotEqual(0, rootInfo?.CreatedAt); 
            
            var statsQuery = new StatsQuery();
            var stats = await bus.QueryUntil(statsQuery, s => s?.NumberOfRoots == numRoots);
            Assert.Equal(numRoots, stats?.NumberOfRoots);
        }

        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await bus.CommandAsync(command);

            var query = new RootInfoQuery("RootCopy");
            var rootInfo = await bus.QueryUntil(query, x => x.CreatedAt > 0); 
            
            Assert.NotEqual(0, rootInfo.CreatedAt);
        }

        [Theory]
        [InlineData(100)]
        public async void CanParallelizeSagas(int numRoots)
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            
            var rootId = numRoots; 
            while (rootId > 0)
            {
                var command = new CreateRoot($"Root{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }

            await bus.QueryUntil(new RootInfoQuery("Root1"));  
            
            rootId = numRoots;
            while (rootId > 0)
            {
                var updateCommand = new UpdateRoot($"Root{rootId}");
                await bus.CommandAsync(updateCommand);
                rootId--;
            }

            await repository.FindUntil<Root>("Root1Copy");
            var stats = await bus.QueryUntil(new StatsQuery(), s => s != null && s.NumberOfRoots == 2 * numRoots, TimeSpan.FromSeconds(2));
            Assert.Equal(2 * numRoots, stats?.NumberOfRoots);
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
            
            var query = new RootInfoQuery("Root1");
            await bus.QueryUntil(query, c => c.CreatedAt > 0);
            
            var statsQuery = new StatsQuery();
            messageQueue.Alert(new InvalidateProjections());
            Thread.Sleep(10);
            var stats = await bus.QueryUntil(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
            Assert.Equal(numberOfRoots, stats?.NumberOfRoots);
            
            var newCommand = new CreateRoot("OtherRoot");
            await bus.CommandAsync(newCommand);
            stats = await bus.QueryUntil(statsQuery, s => s?.NumberOfRoots > numberOfRoots);
            
            Assert.Equal(numberOfRoots + 1, stats?.NumberOfRoots);
        }
    }
}