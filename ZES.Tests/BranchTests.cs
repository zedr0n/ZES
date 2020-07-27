using System;
using System.Collections.Generic;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Branching;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using ZES.Tests.Domain.Sagas;
using static ZES.Interfaces.FastForwardResult;
using static ZES.Utils.ObservableExtensions;

namespace ZES.Tests
{
    public class BranchTests : ZesTest
    {
        public BranchTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [Fact]
        public async void CanMergeTimeline()
        {
            var container = CreateContainer(new List<Action<Container>>() { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var time = container.GetInstance<IBranchManager>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            await time.Branch("test");
            
            await await bus.CommandAsync(new UpdateRoot("Root"));
            
            var infoQuery = new RootInfoQuery("Root");
            await bus.IsTrue(infoQuery, r => r.CreatedAt < r.UpdatedAt);
            
            await await bus.CommandAsync(new CreateRoot("TestRoot"));

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
        public async void CanMergeHistory()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var manager = container.GetInstance<IBranchManager>();
            var queue = container.GetInstance<IMessageQueue>();

            await await bus.CommandAsync(new CreateRecord("Root"));
            await await bus.CommandAsync(new AddRecord("Root", 1));
            await bus.IsTrue(new LastRecordQuery("Root"), r => (int)r.Value == 1);

            var then = ((DateTimeOffset)new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds(); 
            await manager.Branch("Branch", then);
            queue.Alert(new InvalidateProjections());
            await bus.IsTrue(new LastRecordQuery("Root"), r => (int)r.Value == -1);
            
            await await bus.CommandAsync(new CreateRecord("InitialRoot"));
            await await bus.CommandAsync(new AddRecord("InitialRoot", 10));

            manager.Reset();
            
            // await manager.Branch(BranchManager.Master);
            await manager.Merge("Branch");
            
            await bus.IsTrue(new LastRecordQuery("Root"), r => (int)r.Value == 1);
            await bus.IsTrue(new HistoricalQuery<LastRecordQuery, LastRecord>(new LastRecordQuery("Root"), then), r => (int)r.Value == 10);
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
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await manager.Branch("test");
            Assert.Equal("test", manager.ActiveBranch);
            manager.Reset();
            Assert.Equal(BranchManager.Master, manager.ActiveBranch);
            
            await await bus.CommandAsync(new UpdateRoot("Root"));

            await manager.Branch("test");

            Assert.Equal("test", timeline.Id);
            var root = await repository.Find<Root>("Root");
           
            Assert.Equal("Root", root.Id);

            await bus.IsTrue(new RootInfoQuery("Root"), r => r.CreatedAt > 0 && r.CreatedAt == r.UpdatedAt);

            await await bus.CommandAsync(new CreateRoot("TestRoot"));
            await bus.IsTrue(new RootInfoQuery("TestRoot"), r => r.CreatedAt > 0);
            
            Assert.NotNull(locator.Find<TestSaga>("Root"));

            manager.Reset();
            await bus.IsTrue(new RootInfoQuery("Root"), r => r.CreatedAt != r.UpdatedAt);
            await bus.IsTrue(new RootInfoQuery("TestRoot"), r => r.CreatedAt == r.UpdatedAt);

            await manager.Branch("test");
            queue.Alert(new InvalidateProjections());
            await bus.IsTrue(new RootInfoQuery("TestRoot"), r => r.CreatedAt > 0);
            await bus.IsTrue(new RootInfoQuery("Root"), r => r.CreatedAt > 0 && r.CreatedAt == r.UpdatedAt);

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
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot("testRoot"));

            await timeTraveller.Branch("grandTest");
            queue.Alert(new InvalidateProjections());

            await await bus.CommandAsync(new CreateRoot("testRoot2"));
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);
            
            await timeTraveller.Branch("test");
            queue.Alert(new InvalidateProjections());
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
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot("testRoot"));
            timeTraveller.Reset();
            await timeTraveller.Merge("test");

            await timeTraveller.Branch("test");
            await timeTraveller.Branch("grandTest");

            await await bus.CommandAsync(new CreateRoot("testRoot2"));
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 3);

            await timeTraveller.Branch("test");
            queue.Alert(new InvalidateProjections());
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
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            await repository.Find<Root>("Root");
            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);
           
            var timeTraveller = container.GetInstance<IBranchManager>();
            await timeTraveller.Branch("test", 0);
                     
            Assert.Equal("test", timeline.Id);

            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 0);

            timeTraveller.Reset();
            
            Assert.Equal("master", timeline.Id);
            await repository.FindUntil<Root>("Root");
            await bus.IsTrue(new StatsQuery(), s => s.NumberOfRoots == 1);
        }
        
        [Fact]
        public async void CanUseNullRemote()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            await await bus.CommandAsync(new CreateRoot("Root"));

            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);
            
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus);
        }

        [Fact]
        public async void CanPushToRemote()
        {
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            await await bus.CommandAsync(new CreateRoot("Root"));
            await await bus.CommandAsync(new CreateRoot("Root2"));

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
        public async void CanPullFromRemote()
        {
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            await await bus.CommandAsync(new CreateRoot("Root")); 
            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            await await bus.CommandAsync(new UpdateRoot("Root"));
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus); 
        }

        [Fact]
        public async void CanCancelPull()
        {
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var otherContainer = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore(container) });
            
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>();

            await await bus.CommandAsync(new CreateRoot("Root")); 
            var pushResult = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            await await bus.CommandAsync(new UpdateRoot("Root"));

            var otherBus = otherContainer.GetInstance<IBus>();
            var otherRemote = otherContainer.GetInstance<IRemote>();
            await otherRemote.Pull(BranchManager.Master);

            await await otherBus.CommandAsync(new UpdateRoot("Root"));
            await otherRemote.Push(BranchManager.Master);
            
            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Failed, pullResult.ResultStatus);
        }

        [Fact]
        public async void CanPushBranch()
        {
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>(); 
            
            await await bus.CommandAsync(new CreateRoot("Root")); 
            
            var timeTraveller = container.GetInstance<IBranchManager>();
            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot("Root2"));

            var pushResult = await remote.Push("test");
            Assert.Equal(Status.Failed, pushResult.ResultStatus);

            await remote.Push(BranchManager.Master);
            pushResult = await remote.Push("test");
            Assert.Equal(Status.Success, pushResult.ResultStatus);

            var pullResult = await remote.Pull("test");
            Assert.Equal(Status.Success, pullResult.ResultStatus); 
        }

        [Fact]
        public async void CanPushSaga()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas, c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote>(); 
            
            await await bus.CommandAsync(new CreateRoot("Root"));
            var result = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, result.ResultStatus);

            var pullResult = await remote.Pull(BranchManager.Master);
            Assert.Equal(Status.Success, pullResult.ResultStatus);
        }

        [Fact]
        public async void CanPushGrandBranch()
        {
            var container = CreateContainer(new List<Action<Container>> { c => c.UseLocalStore() });
            var bus = container.GetInstance<IBus>();
            var timeTraveller = container.GetInstance<IBranchManager>(); 
            var remote = container.GetInstance<IRemote>(); 
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var result = await remote.Push(BranchManager.Master);
            Assert.Equal(Status.Success, result.ResultStatus); 
            
            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot("testRoot"));

            await timeTraveller.Branch("grandTest");

            await await bus.CommandAsync(new CreateRoot("testRoot2"));
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
    }
}