using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog.Fluent;
using NodaTime;
using NodaTime.Extensions;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Net;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
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
            var id = $"{nameof(CanSaveRoot)}-Root";
            
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var root = await repository.Find<Root>(id);
            Assert.Equal(id, root.Id);
        }

        [Fact]
        public async void CanCalculateStreamHash()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var store = container.GetInstance<IEventStore<IAggregate>>();
            var locator = container.GetInstance<IStreamLocator>();

            var id = $"{nameof(CanCalculateStreamHash)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var stream = await locator.Find<Root>(id);
            var events = await store.ReadStream<IEvent>(stream, 0).ToList();
            Assert.Single(events);
            Assert.NotNull(events.Single().StreamHash);

            var streamHash = await store.GetHash(stream);
            Assert.Equal(events.Single().StreamHash, streamHash);

            await await bus.CommandAsync(new UpdateRoot(id));
            stream = await locator.Find<Root>(id);
            var otherHash = await store.GetHash(stream, 0);
            Assert.Equal(streamHash, otherHash);
        }

        [Fact]
        public async void CanCreateRecord()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanCreateRecord)}-Root";
            await await bus.CommandAsync(new CreateRecord(id));

            var record = await repository.Find<Domain.Record>(id);
            Assert.Equal(id, record.Id);

            await await bus.CommandAsync(new AddRecord(id, 1));
            record = await repository.Find<Domain.Record>(id);
            Assert.Equal(1, record.Values.FirstOrDefault().Value);
        }

        [Fact]
        public async void CanCalculateTotalRecord()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanCalculateTotalRecord)}-Root";
            await await bus.CommandAsync(new CreateRecord(id));

            await await bus.CommandAsync(new AddRecord(id, 1));
            await bus.Equal(new TotalRecordQuery(), r => r.Total, 1.0);

            await await bus.CommandAsync(new AddRecord(id, 2));
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

            var id = $"{nameof(CannotSaveTwice)}-Root";
            var command = new CreateRoot(id) { StoreInLog = false };
            await await bus.CommandAsync(command);
            await bus.Command(command, 2);
            Assert.Equal(nameof(InvalidOperationException), error.ErrorType); 
            Assert.Contains("ahead", error.Message);
            Assert.NotEqual(default, error.Timestamp);

            var failedCommands = await commandLog.FailedCommands.FirstAsync();
            Assert.Single(failedCommands);
        }

        [Fact]
        public async void CanDetectDuplicateCommands()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var errorLog = container.GetInstance<IErrorLog>();
 
            IError error = null;
            errorLog.Observable.Subscribe(e => error = e);
            var id = $"{nameof(CanDetectDuplicateCommands)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            await await bus.CommandAsync(command);
            Assert.NotNull(error); 
        }

        [Fact]
        public async void CanSaveMultipleRoots()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanSaveMultipleRoots)}-Root";
            var command = new CreateRoot($"{id}1");
            await bus.CommandAsync(command);  
            
            var command2 = new CreateRoot($"{id}2");
            await bus.CommandAsync(command2);

            var root = await repository.FindUntil<Root>($"{id}1");
            var root2 = await repository.FindUntil<Root>($"{id}2");

            Assert.NotEqual(root.Id, root2.Id);
        }

        [Fact]
        public async void CanProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanProjectRoot)}-Root";
            var command = new CreateRoot(id);
            await bus.CommandAsync(command); 
            
            await bus.IsTrue(new RootInfoQuery(id), c => c?.CreatedAt != default);
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

            var id = $"{nameof(CanCreateProjectionManager)}-Root";
            var rootInfo = manager.GetProjection<RootInfo>(id);
            var otherRootInfo = manager.GetProjection<RootInfo>(id);
            Assert.Equal(otherRootInfo?.Guid, rootInfo?.Guid);
            
            var rootInfo2 = manager.GetProjection<RootInfo>($"{id}2"); 
            
            Assert.NotEqual(rootInfo?.Guid, rootInfo2.Guid);
        }
        
        [Fact]
        public async void CanUpdateRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repo = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanUpdateRoot)}-Root";
            var command = new CreateRoot(id); 
            await await bus.CommandAsync(command);

            var createdAt = (await bus.QueryUntil(new RootInfoQuery(id), r => r.CreatedAt != default)).CreatedAt;
            
            var updateCommand = new UpdateRoot(id);
            await await bus.CommandAsync(updateCommand);

            await bus.IsTrue(new RootInfoQuery(id), r => r.UpdatedAt > createdAt);

            var root = await repo.Find<Root>(id);
            
            var graph = container.GetInstance<IGraph>();
            await graph.Serialise(nameof(CanUpdateRoot));
        }
        
        [Fact]
        public async void CanDeserializeJustMetadata()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var streamLocator = container.GetInstance<IStreamLocator>();
            var store = container.GetInstance<IEventStore<IAggregate>>();
            var log = container.GetInstance<ILog>();

            var id = $"{nameof(CanUpdateRoot)}-Root";
            var command = new CreateRoot(id); 
            await await bus.CommandAsync(command);

            var updateCommand = new UpdateRoot(id);
            await await bus.CommandAsync(updateCommand);

            var stream = await streamLocator.Find<Root>(id);
            var metadata = await store.ReadStream<IEvent>(stream, 0, 1, SerializationType.Metadata);
            var e = await store.ReadStream<IEvent>(stream, 0, 1);
            Assert.Equal(metadata.MessageId, e.MessageId);
            Assert.Equal(metadata.Version, e.Version);
            Assert.Equal(metadata.Stream, e.Stream);
            
            Assert.Null(metadata.StaticMetadata.CommandId);
            Assert.Null(metadata.StaticMetadata.LocalId);
            Assert.Null(metadata.StaticMetadata.OriginId);

            var copy = e.Copy();
            copy.Version = e.Version + 1;
            await store.AppendToStream(stream, new[] { copy }, false);
            // log.Info(e);
        }
        
        [Theory]
        [InlineData(50000, SerializationType.Metadata)]
        [InlineData(50000, SerializationType.FullMetadata)]
        [InlineData(50000, SerializationType.PayloadAndMetadata)]
        public async void CanDeserializeJustMetadataPerformance(int nLoops, SerializationType serializationType)
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var streamLocator = container.GetInstance<IStreamLocator>();
            var store = container.GetInstance<IEventStore<IAggregate>>();
            var log = container.GetInstance<ILog>();

            var id = $"{nameof(CanUpdateRoot)}-Root";
            var command = new CreateRoot(id); 
            await await bus.CommandAsync(command);

            var updateCommand = new UpdateRoot(id);
            await await bus.CommandAsync(updateCommand);

            var stream = await streamLocator.Find<Root>(id);
            
            var stopWatch = Stopwatch.StartNew();
            for (var i = 0; i < nLoops; i++)
                await store.ReadStream<IEvent>(stream, 0, 1, serializationType);
            stopWatch.Stop();
            log.Info($"{stopWatch.ElapsedMilliseconds}ms per {nLoops} iterations");
            
            /*stopWatch = Stopwatch.StartNew();
            for (var i = 0; i < nLoops; i++)
                await store.ReadStream<IEvent>(stream, 0, 1, SerializationType.FullMetadata);
            stopWatch.Stop();
            log.Info($"{stopWatch.ElapsedMilliseconds}ms per {nLoops} iterations of full metadata");
            
            stopWatch = Stopwatch.StartNew();
            for (var i = 0; i < nLoops; i++)
                await store.ReadStream<IEvent>(stream, 0, 1, SerializationType.PayloadAndMetadata);
            stopWatch.Stop();
            log.Info($"{stopWatch.ElapsedMilliseconds}ms per {nLoops} iterations of full metadata and payload");*/
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
            var id = $"{nameof(CanCreateMultipleRoots)}-Root";
            while ( i < numRoots )
            {
                await await bus.CommandAsync(new CreateRoot($"{id}{i}"));
                i++;
            }
           
            await repository.FindUntil<Root>($"{id}{numRoots - 1}");
            log.Info($"No threading : {stopWatch.ElapsedMilliseconds}ms per {numRoots}");
            
            stopWatch = Stopwatch.StartNew();
            i = 0;
            while ( i < numRoots )
            {
                await bus.CommandAsync(new CreateRoot($"{id}Thread{i}"));
                i++;
            }
            
            await repository.FindUntil<Root>($"{id}Thread{numRoots - 1}");
            log.Info($"Threading : {stopWatch.ElapsedMilliseconds}ms per {numRoots}");
        }

        [Fact]
        public async void CanRecordRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanRecordRoot)}-Root";
            var record = new CreateRecord(id);
            await await bus.CommandAsync(record);

            var time = (DateTime.UtcNow.ToInstant() + Duration.FromMinutes(10)).ToTime(); // (DateTimeOffset)new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc); 
            
            var command = new AddRecord(id, 1) { Timestamp = time };
            await await bus.CommandAsync(command);

            await bus.Equal(new LastRecordQuery(id), c => c.TimeStamp, time);
        }
        
        [Fact]
        public async void CanHistoricalProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();

            var id = $"{nameof(CanHistoricalProjectRoot)}-Root";
            var command = new CreateRoot($"{id}Historical");
            await await bus.CommandAsync(command);

            await await bus.CommandAsync(new CreateRoot($"{id}Temp"));

            var statsQuery = new StatsQuery();
            var now = timeline.Now;
            
            var historicalQuery = new HistoricalQuery<StatsQuery, Stats>(statsQuery, Time.Default);
            await bus.Equal(historicalQuery, s => s.NumberOfRoots, 0);
            
            var liveQuery = new HistoricalQuery<StatsQuery, Stats>(statsQuery, DateTimeOffset.UtcNow.ToInstant().ToTime());
            await bus.Equal(liveQuery, s => s.NumberOfRoots, 2);

            await await bus.CommandAsync(new UpdateRoot($"{id}Historical"));            
            var historicalInfo = new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery($"{id}Historical"), now);
            await bus.IsTrue(historicalInfo, i => i.CreatedAt == i.UpdatedAt);
            await bus.IsTrue(new RootInfoQuery($"{id}Historical"), i => i.UpdatedAt > i.CreatedAt);

            historicalInfo =
                new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery($"{id}Historical"), DateTimeOffset.UtcNow.ToInstant().ToTime());
            await bus.IsTrue(historicalInfo, i => i.UpdatedAt > i.CreatedAt);
            
            historicalInfo =
                new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery($"{id}Temp"), DateTimeOffset.UtcNow.ToInstant().ToTime());
            await bus.IsTrue(historicalInfo, i => i.UpdatedAt == i.CreatedAt);
        }

        [Theory]
        [InlineData(1000)]
        public async void CanProjectALotOfRoots(int numRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var log = container.GetInstance<ILog>();

            var id = $"{nameof(CanProjectALotOfRoots)}-Root";
            var rootId = numRoots; 
            var stopWatch = Stopwatch.StartNew();
            var tasks = new List<Task>();
            while (rootId > 0)
            {
                var command = new CreateRoot($"{id}{rootId}");
                var task = await bus.CommandAsync(command);
                tasks.Add(task);
                rootId--;
            }

            await Task.WhenAll(tasks);
            // await bus.IsTrue(new StatsQuery(), s => s?.NumberOfRoots == numRoots, TimeSpan.FromMilliseconds(numRoots));
            await bus.Equal(new StatsQuery(), s => s?.NumberOfRoots, numRoots);
            await bus.Equal(new RootInfoQuery($"{id}1"), r => r.RootId, $"{id}1");
            log.Info($"{Configuration.ThreadsPerInstance} threads : {stopWatch.ElapsedMilliseconds}ms per {numRoots}");
        }

        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanUseSaga)}";
            var command = new CreateRoot(id);
            await bus.CommandAsync(command);
            await bus.CommandAsync(new UpdateRoot(id));

            await bus.IsTrue(new RootInfoQuery($"{id}Copy"), r => r.UpdatedAt >= r.CreatedAt);
            
            await bus.IsTrue(new RootInfoQuery($"{id}Copy"), r => r.CreatedAt != default);
            await bus.IsTrue(new RootInfoQuery($"{id}Copy"), r => r.UpdatedAt == r.CreatedAt);

            var graph = container.GetInstance<IGraph>();
            await graph.Serialise();
        }

        [Theory]
        [InlineData(10)]
        public async void CanParallelizeSagas(int numRoots)
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanParallelizeSagas)}";
            var rootId = numRoots; 
            while (rootId > 0)
            {
                var command = new CreateRoot($"{id}{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }

            await bus.QueryUntil(new RootInfoQuery($"{id}1"));  
            
            rootId = numRoots;
            while (rootId > 0)
            {
                var updateCommand = new UpdateRoot($"{id}{rootId}");
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

            var id = $"{nameof(CanRebuildProjection)}-Root";
            var rootId = numberOfRoots;
            while (rootId > 0)
            {
                var command = new CreateRoot($"{id}{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }
            
            var query = new RootInfoQuery($"{id}1");
            await bus.QueryUntil(query, c => c.CreatedAt != default);
            messageQueue.Alert(new InvalidateProjections());

            var statsQuery = new StatsQuery();
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
            
            var newCommand = new CreateRoot($"{id}Other");
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

            var id = $"{nameof(CanCancelProjection)}-Root";
            var rootId = numberOfRoots;
            while (rootId > 0)
            {
                var command = new CreateRoot($"{id}{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }
            
            log.Info($"{numberOfRoots} roots submitted");
            
            var statsQuery = new StatsQuery();
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
            
            messageQueue.Alert(new ImmediateInvalidateProjections());
            messageQueue.Alert(new ImmediateInvalidateProjections());
            messageQueue.Alert(new ImmediateInvalidateProjections());
            
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

            var id = $"{nameof(CanCreateSnapshot)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            await await bus.CommandAsync(new UpdateRoot(id));
            await await bus.CommandAsync(new CreateSnapshot<Root>(id));
            await await bus.CommandAsync(new UpdateRoot(id));

            var root = await repository.Find<Root>(id);
            Assert.Equal(2, root.SnapshotVersion);

            await bus.QueryUntil(new RootInfoQuery(id), r => r != null && r.UpdatedAt > r.CreatedAt);
        }

        [Fact]
        public async void CanCreateSagaSnapshot()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<ISaga>>();
            var manager = container.GetInstance<IBranchManager>();
            var id = $"{nameof(CanCreateSagaSnapshot)}-Root";
            
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

            var id = $"{nameof(CanHaveListsInEvents)}-Root"; 
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