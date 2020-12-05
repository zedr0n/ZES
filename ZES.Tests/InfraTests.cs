using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Net;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using ZES.Tests.Domain.Sagas;
using ZES.Utils;

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
        public async void CanCreateRecord()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            await await bus.CommandAsync(new CreateRecord("Root"));

            var record = await repository.Find<Domain.Record>("Root");
            Assert.Equal("Root", record.Id);

            await await bus.CommandAsync(new AddRecord("Root", 1));
            record = await repository.Find<Domain.Record>("Root");
            Assert.Equal(1, record.Values.FirstOrDefault().Value);
        }

        [Fact]
        public async void CanCalculateTotalRecord()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            await await bus.CommandAsync(new CreateRecord("Root"));

            await await bus.CommandAsync(new AddRecord("Root", 1));
            await bus.Equal(new TotalRecordQuery(), r => r.Total, 1.0);

            await await bus.CommandAsync(new AddRecord("Root", 2));
            await bus.Equal(new TotalRecordQuery(), r => r.Total, 3.0);
        }

        [Fact]
        public async void CannotSaveTwice()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var errorLog = container.GetInstance<IErrorLog>();
            var commandLog = container.GetInstance<ICommandLog>();

            IError error = null;
            errorLog.Observable.Subscribe(e => error = e);
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);
            await await bus.CommandAsync(command);
            Assert.Equal(nameof(InvalidOperationException), error.ErrorType); 
            Assert.Contains("ahead", error.Message);
            Assert.NotNull(error.Timestamp);

            var failedCommands = await commandLog.FailedCommands.FirstAsync();
            Assert.Single(failedCommands);
        }

        [Fact]
        public async void CanSaveMultipleRoots()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            
            var command = new CreateRoot("Root1");
            await bus.CommandAsync(command);  
            
            var command2 = new CreateRoot("Root2");
            await bus.CommandAsync(command2);

            var root = await repository.FindUntil<Root>("Root1");
            var root2 = await repository.FindUntil<Root>("Root2");

            Assert.NotEqual(root.Id, root2.Id);
        }

        [Fact]
        public async void CanProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await bus.CommandAsync(command); 
            
            await bus.IsTrue(new RootInfoQuery("Root"), c => c?.CreatedAt != 0);
        }

        [Fact]
        public void CanCreateProjectionManager()
        {
            var container = CreateContainer();
            var manager = container.GetInstance<IProjectionManager>();

            var stats = manager.GetProjection<Stats>();
            Assert.NotNull(stats);

            var otherStats = manager.GetProjection<Stats>();
            Assert.Equal(stats.Guid, otherStats.Guid);

            var rootInfo = manager.GetProjection<RootInfo>("Root");
            var otherRootInfo = manager.GetProjection<RootInfo>("Root");
            Assert.Equal(otherRootInfo?.Guid, rootInfo?.Guid);
            
            var rootInfo2 = manager.GetProjection<RootInfo>("Root2"); 
            
            Assert.NotEqual(rootInfo?.Guid, rootInfo2.Guid);
        }
        
        [Fact]
        public async void CanUpdateRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repo = container.GetInstance<IEsRepository<IAggregate>>();
            
            var command = new CreateRoot("UpdateRoot.Root"); 
            await await bus.CommandAsync(command);

            var createdAt = (await bus.QueryUntil(new RootInfoQuery("UpdateRoot.Root"), r => r.CreatedAt > 0)).CreatedAt;
            
            var updateCommand = new UpdateRoot("UpdateRoot.Root");
            await await bus.CommandAsync(updateCommand);

            await bus.IsTrue(new RootInfoQuery("UpdateRoot.Root"), r => r.UpdatedAt > createdAt);

            var root = await repo.Find<Root>("UpdateRoot.Root");
            
            var graph = container.GetInstance<IGraph>();
            await graph.Serialise(nameof(CanUpdateRoot));
        }

        [Theory]
        [InlineData(1000)]
        public async void CanCreateMultipleRoots(int numRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var log = container.GetInstance<ILog>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var i = 0;
            var stopWatch = Stopwatch.StartNew();
            while ( i < numRoots )
            {
                await await bus.CommandAsync(new CreateRoot($"Root{i}"));
                i++;
            }
            
            log.Info($"No threading : {stopWatch.ElapsedMilliseconds}ms per {numRoots}");
            
            stopWatch = Stopwatch.StartNew();
            i = 0;
            while ( i < numRoots )
            {
                await bus.CommandAsync(new CreateRoot($"ThreadRoot{i}"));
                i++;
            }
            
            await repository.FindUntil<Root>($"ThreadRoot{numRoots - 1}");
            log.Info($"Threading : {stopWatch.ElapsedMilliseconds}ms per {numRoots}");
        }

        [Fact]
        public async void CanRecordRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var record = new CreateRecord("Root");
            await await bus.CommandAsync(record);

            var time = (DateTimeOffset)DateTime.Now.AddMinutes(10); // (DateTimeOffset)new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc); 
            
            var command = new AddRecord("Root", 1) { Timestamp = time.ToUnixTimeMilliseconds() };
            await await bus.CommandAsync(command);
            
            await bus.IsTrue(new LastRecordQuery("Root"), c => c.TimeStamp == time.ToUnixTimeMilliseconds());
        }
        
        [Fact]
        public async void CanHistoricalProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();
            
            var command = new CreateRoot("HistoricalRoot");
            await await bus.CommandAsync(command);

            await await bus.CommandAsync(new CreateRoot("TempRoot"));

            var statsQuery = new StatsQuery();
            var now = timeline.Now;
            
            var historicalQuery = new HistoricalQuery<StatsQuery, Stats>(statsQuery, 0);
            await bus.IsTrue(historicalQuery, s => s.NumberOfRoots == 0);
            
            var liveQuery = new HistoricalQuery<StatsQuery, Stats>(statsQuery, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await bus.IsTrue(liveQuery, s => s.NumberOfRoots == 2);

            await await bus.CommandAsync(new UpdateRoot("HistoricalRoot"));            
            var historicalInfo = new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery("HistoricalRoot"), now);
            await bus.IsTrue(historicalInfo, i => i.CreatedAt == i.UpdatedAt);
            await bus.IsTrue(new RootInfoQuery("HistoricalRoot"), i => i.UpdatedAt > i.CreatedAt);

            historicalInfo =
                new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery("HistoricalRoot"), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await bus.IsTrue(historicalInfo, i => i.UpdatedAt > i.CreatedAt);
            
            historicalInfo =
                new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery("TempRoot"), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await bus.IsTrue(historicalInfo, i => i.UpdatedAt == i.CreatedAt);
        }

        [Theory]
        [InlineData(100)]
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

            // await bus.IsTrue(new StatsQuery(), s => s?.NumberOfRoots == numRoots, TimeSpan.FromMilliseconds(numRoots));
            await bus.Equal(new StatsQuery(), s => s?.NumberOfRoots, numRoots);
            await bus.Equal(new RootInfoQuery("Root1"), r => r.RootId, "Root1");
        }

        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await bus.CommandAsync(command);
            await bus.CommandAsync(new UpdateRoot("Root"));

            await bus.IsTrue(new RootInfoQuery("RootCopy"), r => r.UpdatedAt >= r.CreatedAt);
            
            await bus.IsTrue(new RootInfoQuery("RootCopy"), r => r.CreatedAt > 0);
            await bus.IsTrue(new RootInfoQuery("RootCopy"), r => r.UpdatedAt == r.CreatedAt);

            var graph = container.GetInstance<IGraph>();
            await graph.Serialise();
        }

        [Theory]
        [InlineData(10)]
        public async void CanParallelizeSagas(int numRoots)
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            
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

            await bus.IsTrue(new StatsQuery(), s => s?.NumberOfRoots == 2 * numRoots);

            /*var remote = container.GetInstance<IRemote>();
            await remote.Push(BranchManager.Master);*/
        }
        
        [Theory]
        [InlineData(10)]         
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
            messageQueue.Alert(new InvalidateProjections());

            var statsQuery = new StatsQuery();
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
            
            var newCommand = new CreateRoot("OtherRoot");
            await bus.CommandAsync(newCommand);
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots + 1);
        }
        
        [Theory]
        [InlineData(10)]         
        public async void CanCancelProjection(int numberOfRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var messageQueue = container.GetInstance<IMessageQueue>();
            var log = container.GetInstance<ILog>();

            var rootId = numberOfRoots;
            while (rootId > 0)
            {
                var command = new CreateRoot($"Root{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }
            
            log.Info($"{numberOfRoots} roots submitted");
            
            var statsQuery = new StatsQuery();
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
            
            messageQueue.Alert(new InvalidateProjections());
            messageQueue.Alert(new InvalidateProjections());
            messageQueue.Alert(new InvalidateProjections());
            
            Thread.Sleep(50);
            
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
        }

        [Fact]
        public async void CanRequestJson()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var queue = container.GetInstance<IMessageQueue>();

            const string url = "https://api.coingecko.com/api/v3/coins/bitcoin/history?date=30-12-2017&localization=false";
            await await bus.CommandAsync(new RequestJson(nameof(CanRequestJson), url));
            await await bus.CommandAsync(new RequestJson(nameof(CanRequestJson), url));

            var res = await queue.Alerts.OfType<JsonRequestCompleted>().FirstAsync().Timeout(Configuration.Timeout);
            Assert.NotNull(res.JsonData); 
            Assert.Contains("13620.3618741461", res.JsonData);
        }
        
        [Fact]
        public async void CanDeserializeRequestedJson()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var queue = container.GetInstance<IMessageQueue>();

            const string url = "https://api.coingecko.com/api/v3/coins/bitcoin/history?date=30-12-2017&localization=false";
            await await bus.CommandAsync(new RequestJson<TestJson>(nameof(CanDeserializeRequestedJson), url));

            var res = await queue.Alerts.OfType<JsonRequestCompleted<TestJson>>().FirstAsync().Timeout(Configuration.Timeout);
            Assert.NotNull(res.Data);
            Assert.Equal("btc", res.Data.Symbol);
        }

        [Fact]
        public async void CanCreateSnapshot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            await await bus.CommandAsync(new CreateRoot("Root"));
            await await bus.CommandAsync(new UpdateRoot("Root"));
            await await bus.CommandAsync(new CreateSnapshot<Root>("Root"));

            var root = await repository.Find<Root>("Root");
            Assert.Equal(2, root.SnapshotVersion);
        }

        [Fact]
        public async void CanCreateSagaSnapshot()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<ISaga>>();
            var manager = container.GetInstance<IBranchManager>();
            var id = "SnapshotRoot";
            
            await await bus.CommandAsync(new CreateRoot(id));
            await await bus.CommandAsync(new UpdateRoot(id));

            await manager.Ready;
            
            var saga = await repository.Find<TestSaga>(id);
            Assert.Equal(0, saga.SnapshotVersion);

            await await bus.CommandAsync(new CreateSnapshot<Root>(id));

            await manager.Ready;
            
            saga = await repository.Find<TestSaga>(id);
            Assert.Equal(2, saga.SnapshotVersion);
            Assert.Equal(TestSaga.State.Complete, saga.CurrentState);
        }

        [Fact]
        public async void CanHaveListsInEvents()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var id = nameof(CanHaveListsInEvents); 
            await await bus.CommandAsync(new CreateRoot(id));

            var lst = new List<string>()
            {
                "a",
                "b",
                "c",
            };

            await await bus.CommandAsync(new AddRootDetails(id, lst.ToArray()));
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var root = await repository.Find<Root>(id);
            
            Assert.Equal(3, root.Details.Count);
        }
    }
}