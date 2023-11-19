using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NodaTime;
using NodaTime.Extensions;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Branching;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Infrastructure.Stochastics;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Replicas;
using ZES.Interfaces.Stochastic;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using ZES.Tests.Domain.Sagas;
using ZES.Tests.Domain.Stochastics;
using static ZES.Interfaces.FastForwardResult;
using static ZES.Utils.ObservableExtensions;

#pragma warning disable SA1600

namespace ZES.Tests
{
    public class BranchTests : ZesTest
    {
        public BranchTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [Fact]
        public async void CanBranchWithMultipleAncestors()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>();
            var locator = container.GetInstance<IStreamLocator>();

            var id = $"{nameof(CanBranchWithMultipleAncestors)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            await manager.Branch("test");

            await await bus.CommandAsync(new UpdateRoot(id));
            await manager.Branch("test2");
            
            await await bus.CommandAsync(new UpdateRoot(id));

            var stream = await locator.Find<Root>(id, "test2");
            Assert.Equal(2, stream.Ancestors.Count());
            
            var streamBranch = stream.Branch("test3", 1);
            Assert.Equal("test", streamBranch.Parent.Timeline);
            Assert.Equal(1, streamBranch.Version);
        }

        [Fact]
        public async void CanCorrectlyGetBranchStreamHash()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            var store = container.GetInstance<IEventStore<IAggregate>>();
            var locator = container.GetInstance<IStreamLocator>();
            var timeline = container.GetInstance<ITimeline>();

            var manager = container.GetInstance<IBranchManager>();

            var id = $"{nameof(CanCorrectlyGetBranchStreamHash)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            var stream = await locator.Find<Root>(id);
            var rootCreatedHash = await store.GetHash(stream);
            var now = timeline.Now;
            
            await await bus.CommandAsync(new UpdateRoot(id));
            stream = await locator.Find<Root>(id);
            var rootUpdatedHash = await store.GetHash(stream);

            var branch = await manager.Branch("test", now);
            var branchStream = await locator.FindBranched(stream, "test");
            var branchHash = await store.GetHash(branchStream);
            Assert.Equal(rootCreatedHash, branchHash);
            Assert.NotEqual(rootUpdatedHash, branchHash);

            var otherStream = stream.Branch("test2", 0);
            var otherHash = await store.GetHash(otherStream);
            Assert.Equal(rootCreatedHash, otherHash);

            var latestStream = stream.Branch("test3", 1);
            var latestHash = await store.GetHash(latestStream);
            Assert.Equal(rootUpdatedHash, latestHash);
        }

        [Fact]
        public async void CanGetEmptyStreamHash()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var locator = container.GetInstance<IStreamLocator>();
            var store = container.GetInstance<IEventStore<IAggregate>>();

            var id = $"{nameof(CanGetEmptyStreamHash)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            var stream = await locator.Find<Root>(id);
            var emptyStream = stream.Branch("test", ExpectedVersion.EmptyStream);
            var hash = await store.GetHash(emptyStream);
            Assert.Empty(hash);
        }
        
        [Fact]
        public async void CanMergeTimeline()
        {
            var container = CreateContainer(new List<Action<Container>>() { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var time = container.GetInstance<IBranchManager>();

            var id = $"{nameof(CanMergeTimeline)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            await time.Branch("test");
            
            await await bus.CommandAsync(new UpdateRoot(id));
            
            var infoQuery = new RootInfoQuery(id);
            await bus.IsTrue(infoQuery, r => r.CreatedAt < r.UpdatedAt);
            
            await await bus.CommandAsync(new CreateRoot($"{id}Test"));

            time.Reset();
            
            await bus.IsTrue(infoQuery, r => r.CreatedAt == r.UpdatedAt);
            
            await time.Merge("test");

            await bus.IsTrue(infoQuery, r => r.CreatedAt < r.UpdatedAt);
            await bus.Equal(new StatsQuery(), s => s.NumberOfRoots, 4);

            await time.Merge("test");
            await bus.Equal(new StatsQuery(), s => s.NumberOfRoots, 4);

            var graph = container.GetInstance<IGraph>();
            await graph.Serialise(nameof(CanMergeTimeline));
        }

        [Fact]
        public async void CanUpdateTimeline()
        {
            var container = CreateContainer(new List<Action<Container>>() { });
            var bus = container.GetInstance<IBus>();
            var time = container.GetInstance<IBranchManager>();

            var id = $"{nameof(CanUpdateTimeline)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            await time.Branch("test");
            time.Reset();
            
            await await bus.CommandAsync(new UpdateRoot(id));
            await time.Branch("test");
            var mergeResult = await time.Merge(BranchManager.Master);
            Assert.True(mergeResult.Success);
            Assert.Equal(1, mergeResult.Changes.SingleOrDefault().Value);
        }

        [Fact]
        public async void CanMergeWithoutNewStreams()
        {
            var container = CreateContainer(new List<Action<Container>>() { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>();

            var id = $"{nameof(CanMergeWithoutNewStreams)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);
            
            await manager.Branch("test");
            
            await await bus.CommandAsync(new UpdateRoot(id));
            
            var infoQuery = new RootInfoQuery(id);
            await bus.IsTrue(infoQuery, r => r.CreatedAt < r.UpdatedAt);

            await await bus.CommandAsync(new CreateRoot($"{id}Branch"));

            manager.Reset();
            
            await bus.IsTrue(infoQuery, r => r.CreatedAt == r.UpdatedAt);

            await manager.Merge("test", false);
            
            await bus.IsTrue(infoQuery, r => r.CreatedAt < r.UpdatedAt);
            await bus.Equal(new StatsQuery(), s => s.NumberOfRoots, 2);
        }

        [Fact]
        public async void CanQueryTimeline()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>();

            var id = $"{nameof(CanQueryTimeline)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            await bus.Equal(new StatsQuery(), s => s.NumberOfRoots, 1);

            await manager.Branch("Branch");
            await await bus.CommandAsync(new CreateRoot($"{id}Other"));
            await bus.Equal(new StatsQuery(), s => s.NumberOfRoots, 2);

            manager.Reset();
            
            await bus.Equal(new StatsQuery(), s => s.NumberOfRoots, 1);
            await bus.Equal(new StatsQuery { Timeline = "Branch" }, s => s.NumberOfRoots, 2);
            await bus.Equal(new StatsQuery { Timeline = "Test" }, s => s.NumberOfRoots, 0);
            await bus.Equal(new StatsQuery { Timeline = BranchManager.Master }, s => s.NumberOfRoots, 1);
        }

        [Fact]
        public async void CanMergeHistory()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>();
            var queue = container.GetInstance<IMessageQueue>();

            var id = $"{nameof(CanMergeHistory)}-Root";
            await await bus.CommandAsync(new CreateRecord(id));
            await await bus.CommandAsync(new AddRecord(id, 1));
            await bus.IsTrue(new LastRecordQuery(id), r => (int)r.Value == 1);

            var then = new DateTime(1970, 1, 1, 12, 0, 0, DateTimeKind.Utc).ToInstant().ToTime(); 
            await manager.Branch("Branch", then);
            queue.Alert(new InvalidateProjections());
            await bus.Equal(new LastRecordQuery(id), r => r.Value, -1);
            
            await await bus.CommandAsync(new CreateRecord($"{id}Initial"));
            await await bus.CommandAsync(new AddRecord($"{id}Initial", 10));

            manager.Reset();
            
            // await manager.Branch(BranchManager.Master);
            await manager.Merge("Branch");
            
            await bus.IsTrue(new LastRecordQuery(id), r => (int)r.Value == 1);
            await bus.IsTrue(new HistoricalQuery<LastRecordQuery, LastRecord>(new LastRecordQuery(id), then), r => (int)r.Value == 10);
        }
        
        [Fact]
        public async void CanCreateClone()
        {
            var container = CreateContainer(new List<Action<Container>>() { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            var manager = container.GetInstance<IBranchManager>();
            var locator = container.GetInstance<IStreamLocator>();
            var queue = container.GetInstance<IMessageQueue>();

            var id = $"{nameof(CanCreateClone)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await manager.Branch("test");
            Assert.Equal("test", manager.ActiveBranch);
            manager.Reset();
            Assert.Equal(BranchManager.Master, manager.ActiveBranch);
            
            await await bus.CommandAsync(new UpdateRoot(id));

            await manager.Branch("test");

            Assert.Equal("test", timeline.Id);
            var root = await repository.Find<Root>(id);
           
            Assert.Equal(id, root.Id);

            await bus.IsTrue(new RootInfoQuery(id), r => r.CreatedAt != default && r.CreatedAt == r.UpdatedAt);

            await await bus.CommandAsync(new CreateRoot($"{id}Test"));
            await bus.IsTrue(new RootInfoQuery($"{id}Test"), r => r.CreatedAt != default);
            
            Assert.NotNull(locator.Find<TestSaga>(id));

            manager.Reset();
            await bus.IsTrue(new RootInfoQuery(id), r => r.CreatedAt != r.UpdatedAt);
            await bus.IsTrue(new RootInfoQuery($"{id}Test"), r => r.CreatedAt == r.UpdatedAt);

            await manager.Branch("test");
            queue.Alert(new ImmediateInvalidateProjections());
            await bus.IsTrue(new RootInfoQuery($"{id}Test"), r => r.CreatedAt != default);
            await bus.IsTrue(new RootInfoQuery(id), r => r.CreatedAt != default && r.CreatedAt == r.UpdatedAt);

            var graph = container.GetInstance<IGraph>();
            await graph.Serialise();
        }

        [Fact]
        public async void CanMergeGrandTimeline()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeTraveller = container.GetInstance<IBranchManager>();
            var queue = container.GetInstance<IMessageQueue>();

            var id = $"{nameof(CanMergeGrandTimeline)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot($"{id}Test"));

            await timeTraveller.Branch("grandTest");

            await await bus.CommandAsync(new CreateRoot($"{id}Test2"));
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);
            
            await timeTraveller.Branch("test");
            queue.Alert(new ImmediateInvalidateProjections());
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 2);

            await timeTraveller.Merge("grandTest");
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);

            timeTraveller.Reset();
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 1);

            await timeTraveller.Merge("test");
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);

            var graph = container.GetInstance<IGraph>();
            await graph.Populate();
            await graph.Serialise();
        }

        [Fact]
        public async void CanMergeGrandTimelineSequentially()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeTraveller = container.GetInstance<IBranchManager>();
            var queue = container.GetInstance<IMessageQueue>();

            var id = $"{nameof(CanMergeGrandTimelineSequentially)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot($"{id}Test"));
            timeTraveller.Reset();
            await timeTraveller.Merge("test");

            await timeTraveller.Branch("test");
            await timeTraveller.Branch("grandTest");

            await await bus.CommandAsync(new CreateRoot($"{id}Test2"));
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);

            await timeTraveller.Branch("test");
            queue.Alert(new ImmediateInvalidateProjections());
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 2);

            await timeTraveller.Merge("grandTest");
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);

            timeTraveller.Reset();
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 2);

            await timeTraveller.Merge("test");
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);
        }
        
        [Fact]
        public async void CanCreateEmpty()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanCreateEmpty)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            await repository.Find<Root>(id);
            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);
           
            var timeTraveller = container.GetInstance<IBranchManager>();
            await timeTraveller.Branch("test", Time.MinValue);
                     
            Assert.Equal("test", timeline.Id);

            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 0);

            timeTraveller.Reset();
            
            Assert.Equal("master", timeline.Id);
            await repository.FindUntil<Root>(id);
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 1);
        }
        
        [Fact]
        public async void CanUseNullRemote()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            var id = $"{nameof(CanUseNullRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));

            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);
            
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus);
        }

        [Fact]
        public async void CanPushToRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;

            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            var id = $"{nameof(CanPushToRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            await await bus.CommandAsync(new CreateRoot($"{id}2"));

            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);
            
            // +1 because of command log
            Assert.Equal(3, pushResult.NumberOfStreams);
            Assert.Equal(4, pushResult.NumberOfMessages);

            // remote is synced so nothing to pull
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus); 
            Assert.Equal(0, pullResult.NumberOfStreams );
            Assert.Equal(0, pullResult.NumberOfMessages);
        }
        
        [Fact]
        public async void CanPushToGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(resetDb: true);
            var bus = container.GetInstance<IBus>();
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");

            var id = $"{nameof(CanPushToGenericRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            await await bus.CommandAsync(new CreateRoot($"{id}2"));

            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);
            
            // +1 because of command log
            Assert.Equal(3, pushResult.NumberOfStreams);
            Assert.Equal(4, pushResult.NumberOfMessages);

            // remote is synced so nothing to pull
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus); 
            Assert.Equal(0, pullResult.NumberOfStreams );
            Assert.Equal(0, pullResult.NumberOfMessages);
        }

        [Fact]
        public async void CanCancelPushToGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(resetDb: true);
            var bus = container.GetInstance<IBus>();
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");

            var id = $"{nameof(CanCancelPushToGenericRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            await remote.Push(BranchManager.Master);

            var otherContainer = CreateContainer(db: 1);
            var otherRemoteManager = otherContainer.GetInstance<IRemoteManager>();
            otherRemoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var otherRemote = otherRemoteManager.GetGenericRemote("Server");
            await otherRemote.Pull(BranchManager.Master);
            
            var otherBus = otherContainer.GetInstance<IBus>();
            await await otherBus.CommandAsync(new UpdateRoot(id));

            await otherRemote.Push(BranchManager.Master);
           
            await await bus.CommandAsync(new UpdateRoot(id));
            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Failed, pushResult.ResultStatus);
        }

        [Fact]
        public async void CanUpdateGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(resetDb: true);
            var bus = container.GetInstance<IBus>();
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");

            var id = $"{nameof(CanUpdateGenericRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));

            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);
            
            // +1 because of command log
            Assert.Equal(2, pushResult.NumberOfStreams);
            Assert.Equal(2, pushResult.NumberOfMessages);

            await await bus.CommandAsync(new UpdateRoot(id));
            var pushResultAfter = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResultAfter.ResultStatus);
            Assert.Equal(2, pushResult.NumberOfStreams);
            Assert.Equal(2, pushResult.NumberOfMessages);
            
            var otherContainer = CreateContainer(db: 1);
            var otherBus = otherContainer.GetInstance<IBus>();
            var otherManager = otherContainer.GetInstance<IBranchManager>();
            var otherRemoteManager = otherContainer.GetInstance<IRemoteManager>();
            otherRemoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var otherRemote = otherRemoteManager.GetGenericRemote("Server");
            await otherRemote.Pull(BranchManager.Master);
            
            await otherBus.IsTrue(new RootInfoQuery(id), s => s.UpdatedAt > s.CreatedAt);
        }

        [Fact]
        public async void CanPullFromRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            var id = $"{nameof(CanPullFromRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id)); 
            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            await await bus.CommandAsync(new UpdateRoot(id));
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus); 
            Assert.Equal(1, pullResult.NumberOfStreams);
        }
        
        [Fact]
        public async void CanPullFromGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(resetDb: true);
            var bus = container.GetInstance<IBus>();
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");

            var id = $"{nameof(CanPullFromGenericRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id)); 
            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            var otherContainer = CreateContainer(db: 1);
            var otherRemoteManager = otherContainer.GetInstance<IRemoteManager>();
            otherRemoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var otherRemote = otherRemoteManager.GetGenericRemote("Server");

            var pullResult = await otherRemote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus); 
            Assert.Equal(2, pullResult.NumberOfStreams);
            Assert.Equal(2, pullResult.NumberOfMessages);
            
            var repository = otherContainer.GetInstance<IEsRepository<IAggregate>>();
            var root = await repository.Find<Root>(id);
            Assert.Equal(id, root.Id);
        }

        [Fact]
        public async void CanCancelPull()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var otherContainer = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore(container) });
            
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            var id = $"{nameof(CanCancelPull)}-Root";
            await await bus.CommandAsync(new CreateRoot(id)); 
            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            await await bus.CommandAsync(new UpdateRoot(id));

            var otherBus = otherContainer.GetInstance<IBus>();
            var otherRemote = otherContainer.GetInstance<IRemote>();
            await otherRemote.Pull(BranchManager.Master);

            await await otherBus.CommandAsync(new UpdateRoot(id));
            await otherRemote.Push(BranchManager.Master);
            
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Failed, pullResult.ResultStatus);
        }
        
        [Fact]
        public async void CanCancelPullFromGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(resetDb: true);
            
            var bus = container.GetInstance<IBus>();
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");

            var id = $"{nameof(CanCancelPullFromGenericRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id)); 
            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            await await bus.CommandAsync(new UpdateRoot(id));

            var otherContainer = CreateContainer(db: 1);
            var otherBus = otherContainer.GetInstance<IBus>();
            var otherRemoteManager = otherContainer.GetInstance<IRemoteManager>();
            otherRemoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var otherRemote = otherRemoteManager.GetGenericRemote("Server");
            await otherRemote.Pull(BranchManager.Master);

            await await otherBus.CommandAsync(new UpdateRoot(id));
            await otherRemote.Push(BranchManager.Master);
            
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Failed, pullResult.ResultStatus);
        }

        [Fact]
        public async void CanCancelMerge()
        {
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanCancelMerge)}-Root";
            await bus.Command(new CreateRoot(id));
            await manager.Branch("test");
            await bus.Command(new UpdateRoot(id));
            await bus.Command(new CreateRoot($"{id}Other"));

            var otherRoot = await repository.Find<Root>($"{id}Other");
            Assert.NotNull(otherRoot);

            manager.Reset();

            await bus.Command(new UpdateRoot(id));
            var mergeResult = await manager.Merge("test");
            Assert.False(mergeResult.Success);

            otherRoot = await repository.Find<Root>($"{id}Other");
            Assert.Null(otherRoot);
        }

        [Fact]
        public async void CanPushBranch()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            var id = $"{nameof(CanPushBranch)}-Root";
            await await bus.CommandAsync(new CreateRoot(id)); 
            
            var timeTraveller = container.GetInstance<IBranchManager>();
            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot($"{id}2"));

            var pushResult = await remote.Push("test");
            Assert.Equal(Status.Failed, pushResult.ResultStatus);

            await remote.Push(BranchManager.Master);
            pushResult = await remote.Push("test");
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            var pullResult = await remote.Pull("test");
            Assert.Equal(Status.Success, pullResult.ResultStatus); 
        }
        
        [Fact]
        public async void CanPushBranchToGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(resetDb: true);
            var bus = container.GetInstance<IBus>();
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");

            var id = $"{nameof(CanPushBranchToGenericRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id)); 
            
            var timeTraveller = container.GetInstance<IBranchManager>();
            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot($"{id}2"));

            var pushResult = await remote.Push("test");
            Assert.Equal(Status.Failed, pushResult.ResultStatus);

            await remote.Push(BranchManager.Master);
            pushResult = await remote.Push("test");
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            var pullResult = await remote.Pull("test");
            Assert.Equal(Status.Success, pullResult.ResultStatus); 
            
            var otherContainer = CreateContainer(db: 1);
            var otherBus = otherContainer.GetInstance<IBus>();
            var otherManager = otherContainer.GetInstance<IBranchManager>();
            var otherRemoteManager = otherContainer.GetInstance<IRemoteManager>();
            otherRemoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var otherRemote = otherRemoteManager.GetGenericRemote("Server");
            await otherRemote.Pull(BranchManager.Master);
            await otherRemote.Pull("test");

            await otherManager.Branch("test");
            
            await otherBus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 2);
        }

        [Fact]
        public async void CanPushSaga()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;

            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas, c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            var id = $"{nameof(CanPushSaga)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            var result = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, result.ResultStatus);
            Assert.Equal(6, result.NumberOfMessages);
            Assert.Equal(5, result.NumberOfStreams);

            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus);
            Assert.Equal(0, pullResult.NumberOfMessages);
            Assert.Equal(0, pullResult.NumberOfStreams);
        }
        
        [Fact]
        public async void CanPushSagaToGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(registrations: new List<Action<Container>> { Config.RegisterSagas }, resetDb: true);
            var bus = container.GetInstance<IBus>();
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");

            var id = $"{nameof(CanPushSagaToGenericRemote)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            var result = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, result.ResultStatus);
            Assert.Equal(6, result.NumberOfMessages);
            Assert.Equal(5, result.NumberOfStreams);

            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus);
            Assert.Equal(0, pullResult.NumberOfMessages);
            Assert.Equal(0, pullResult.NumberOfStreams);
            
            var otherContainer = CreateContainer(db: 1);
            var otherBus = otherContainer.GetInstance<IBus>();
            var otherRemoteManager = otherContainer.GetInstance<IRemoteManager>();
            otherRemoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var otherRemote = otherRemoteManager.GetGenericRemote("Server");
            await otherRemote.Pull(BranchManager.Master);
            
            await otherBus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 2);
        }

        [Fact]
        public async void CanPushGrandBranch()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var timeTraveller = container.GetInstance<IBranchManager>(); 
            var remote = container.GetInstance<IRemote>();

            var id = $"{nameof(CanPushGrandBranch)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var result = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, result.ResultStatus); 
            
            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot($"{id}Test"));

            await timeTraveller.Branch("grandTest");

            await await bus.CommandAsync(new CreateRoot($"{id}Test2"));
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);

            result = await remote.Push("grandTest");
            Assert.Equal(Status.Failed, result.ResultStatus);

            result = await remote.Push("test");
            Assert.Equal(Status.Success, result.ResultStatus);
            
            result = await remote.Push("grandTest");
            Assert.Equal(Status.Success, result.ResultStatus);

            result = await remote.Pull("grandTest");
            Assert.Equal(Status.Success, result.ResultStatus);
        }
        
        [Fact]
        public async void CanPushGrandBranchToGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(resetDb: true);
            var bus = container.GetInstance<IBus>();
            var timeTraveller = container.GetInstance<IBranchManager>(); 
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");

            var id = $"{nameof(CanPushGrandBranchToGenericRemote)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var result = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, result.ResultStatus); 
            
            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot($"{id}Test"));

            await timeTraveller.Branch("grandTest");

            await await bus.CommandAsync(new CreateRoot($"{id}Test2"));
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);

            result = await remote.Push("grandTest");
            Assert.Equal(Status.Failed, result.ResultStatus);

            result = await remote.Push("test");
            Assert.Equal(Status.Success, result.ResultStatus);
            
            result = await remote.Push("grandTest");
            Assert.Equal(Status.Success, result.ResultStatus);

            result = await remote.Pull("grandTest");
            Assert.Equal(Status.Success, result.ResultStatus);
            
            var otherContainer = CreateContainer(db: 1);
            var otherBus = otherContainer.GetInstance<IBus>();
            var otherManager = otherContainer.GetInstance<IBranchManager>(); 
            var otherRemoteManager = otherContainer.GetInstance<IRemoteManager>();
            otherRemoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var otherRemote = otherRemoteManager.GetGenericRemote("Server");
            await otherRemote.Pull(BranchManager.Master);
            await otherRemote.Pull("test");
            await otherRemote.Pull("grandTest");

            await otherManager.Branch("grandTest");

            await otherBus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);
        }

        [Fact]
        public async void CanPushSnapshot()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;

            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>(); 
            var remote = container.GetInstance<IRemote>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanPushSnapshot)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);
            await await bus.CommandAsync(new UpdateRoot(id));

            await await bus.CommandAsync(new CreateSnapshot<Root>(id));

            var result = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, result.ResultStatus);
            
            await manager.Branch("test");
            await await bus.CommandAsync(new UpdateRoot(id));

            result = await remote.Push("test");
            Assert.Equal(Status.Success, result.ResultStatus);

            var root = await repository.Find<Root>(id);
            Assert.Equal(3, root.Version);
            Assert.Equal(2, root.SnapshotVersion);
        }
        
        [Fact]
        public async void CanPushSnapshotToGenericRemote()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer(resetDb: true);
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>(); 
            var remoteManager = container.GetInstance<IRemoteManager>();
            var localReplica = container.GetInstance<IFactory<LocalReplica>>().Create();
            remoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var remote = remoteManager.GetGenericRemote("Server");
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanPushSnapshotToGenericRemote)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);
            await await bus.CommandAsync(new UpdateRoot(id));

            await await bus.CommandAsync(new CreateSnapshot<Root>(id));

            var result = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, result.ResultStatus);
            
            await manager.Branch("test");
            await await bus.CommandAsync(new UpdateRoot(id));

            result = await remote.Push("test");
            Assert.Equal(Status.Success, result.ResultStatus);

            var root = await repository.Find<Root>(id);
            Assert.Equal(3, root.Version);
            Assert.Equal(2, root.SnapshotVersion);
            
            var otherContainer = CreateContainer(db: 1);
            var otherRepository = otherContainer.GetInstance<IEsRepository<IAggregate>>();
            var otherManager = otherContainer.GetInstance<IBranchManager>(); 
            var otherRemoteManager = otherContainer.GetInstance<IRemoteManager>();
            otherRemoteManager.RegisterLocalReplica("Server", localReplica.AggregateEventStore, localReplica.SagaEventStore, localReplica.CommandLog);
            var otherRemote = otherRemoteManager.GetGenericRemote("Server");
            var pullResult = await otherRemote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus);

            var otherRoot = await otherRepository.Find<Root>(id);
            Assert.Equal(2, otherRoot.Version);
            Assert.Equal(2, otherRoot.SnapshotVersion);

            await otherRemote.Pull("test");
            await otherManager.Branch("test");
            otherRoot = await otherRepository.Find<Root>(id);
            Assert.Equal(3, otherRoot.Version);
            Assert.Equal(2, otherRoot.SnapshotVersion);
        }

        [Fact]
        public void CanDoRecordAction()
        {
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>();
            var process = new MarkovDecisionProcessBase<BranchState>(new BranchState(BranchManager.Master), 100)
            {
                Rewards = new List<IActionReward<BranchState>> { new RecordReward(bus) },
            };
            var policy = new RecordPolicy(bus, manager);
            var result = process.GetOptimalValue(policy);
            
            Assert.Equal(10, result);
        }
    }
}