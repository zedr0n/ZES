using System;
using System.Collections.Generic;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure.Branching;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
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
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var time = container.GetInstance<IBranchManager>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            await time.Branch("test");
            
            await await bus.CommandAsync(new UpdateRoot("Root"));
            
            var rootInfo = await bus.QueryUntil(new RootInfoQuery("Root"));
            Assert.True(rootInfo.CreatedAt < rootInfo.UpdatedAt);
            
            await await bus.CommandAsync(new CreateRoot("TestRoot"));

            time.Reset();
            
            rootInfo = await bus.QueryAsync(new RootInfoQuery("Root"));
            Assert.True(rootInfo.CreatedAt == rootInfo.UpdatedAt); 
            
            await time.Merge("test");

            rootInfo = await bus.QueryAsync(new RootInfoQuery("Root"));
            Assert.True(rootInfo.CreatedAt < rootInfo.UpdatedAt);  
            rootInfo = await bus.QueryAsync(new RootInfoQuery("TestRoot"));
            Assert.NotNull(rootInfo);
            var stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 2);
            Assert.Equal(2, stats.NumberOfRoots);

            await time.Merge("test");
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 2);
            Assert.Equal(2, stats.NumberOfRoots);
        }
        
        [Fact]
        public async void CanCreateClone()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            var timeTraveller = container.GetInstance<IBranchManager>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");
            timeTraveller.Reset();
            
            await await bus.CommandAsync(new UpdateRoot("Root"));

            await timeTraveller.Branch("test");

            Assert.Equal("test", timeline.Id);
            var root = await repository.Find<Root>("Root");
           
            Assert.Equal("Root", root.Id);

            var rootInfo = await bus.QueryUntil(new RootInfoQuery("Root"));
            Assert.True(rootInfo?.CreatedAt > 0);
            Assert.True(rootInfo.CreatedAt == rootInfo.UpdatedAt);

            await await bus.CommandAsync(new CreateRoot("TestRoot"));
            var testRootInfo = await bus.QueryUntil(new RootInfoQuery("TestRoot"), r => r?.CreatedAt > 0);
            Assert.True(testRootInfo?.CreatedAt > 0);

            timeTraveller.Reset();
            rootInfo = await bus.QueryAsync(new RootInfoQuery("Root"));
            Assert.True(rootInfo.CreatedAt != rootInfo.UpdatedAt);

            testRootInfo = await bus.QueryAsync(new RootInfoQuery("TestRoot"));
            Assert.True(testRootInfo.CreatedAt == testRootInfo.UpdatedAt);

            await timeTraveller.Branch("test");
            testRootInfo = await bus.QueryUntil(new RootInfoQuery("TestRoot"), r => r?.CreatedAt > 0);
            Assert.True(testRootInfo?.CreatedAt > 0); 
            
            rootInfo = await bus.QueryAsync(new RootInfoQuery("Root"));
            Assert.True(rootInfo.UpdatedAt == rootInfo.CreatedAt);
            Assert.NotEqual(0, rootInfo.CreatedAt);
        }

        [Fact]
        public async void CanMergeGrandTimeline()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeTraveller = container.GetInstance<IBranchManager>(); 
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            await timeTraveller.Branch("test");

            await await bus.CommandAsync(new CreateRoot("testRoot"));

            await timeTraveller.Branch("grandTest");

            await await bus.CommandAsync(new CreateRoot("testRoot2"));

            var stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 3);
            Assert.Equal(3, stats.NumberOfRoots);

            await timeTraveller.Branch("test");
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 2);
            Assert.Equal(2, stats.NumberOfRoots);

            await timeTraveller.Merge("grandTest");
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 3); 
            Assert.Equal(3, stats.NumberOfRoots);

            timeTraveller.Reset();
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 1); 
            Assert.Equal(1, stats.NumberOfRoots);

            await timeTraveller.Merge("test");
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 3); 
            Assert.Equal(3, stats.NumberOfRoots); 
        }

        [Fact]
        public async void CanMergeGrandTimelineSequentially()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeTraveller = container.GetInstance<IBranchManager>(); 
            
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

            var stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 3);
            Assert.Equal(3, stats.NumberOfRoots);

            await timeTraveller.Branch("test");
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 2);
            Assert.Equal(2, stats.NumberOfRoots);

            await timeTraveller.Merge("grandTest");
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 3); 
            Assert.Equal(3, stats.NumberOfRoots);

            timeTraveller.Reset();
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 2); 
            Assert.Equal(2, stats.NumberOfRoots);

            await timeTraveller.Merge("test");
            stats = await bus.QueryUntil(new StatsQuery(), s => s?.NumberOfRoots == 3); 
            Assert.Equal(3, stats.NumberOfRoots); 
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

            var query = new StatsQuery();
            var stats = await bus.QueryUntil(query, s => s?.NumberOfRoots == 0);
            Assert.Equal(0, stats.NumberOfRoots);

            timeTraveller.Reset();
            
            Assert.Equal("master", timeline.Id);
            await repository.FindUntil<Root>("Root");
            stats = await bus.QueryUntil(query, s => s?.NumberOfRoots == 1);
            Assert.Equal(1, stats?.NumberOfRoots); 
        }
        
        [Fact]
        public async void CanUseNullRemote()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote<IAggregate>>();

            await await bus.CommandAsync(new CreateRoot("Root"));

            await remote.Push(BranchManager.Master);
            await remote.Pull(BranchManager.Master);
        }

        [Fact]
        public async void CanPushToRemote()
        {
            var container = CreateContainer(new List<Action<Container>>
            {
                c =>
                {
                    c.Options.AllowOverridingRegistrations = true;
                    c.Register(typeof(IRemote<>), typeof(Remote<>), Lifestyle.Singleton);
                    c.Options.AllowOverridingRegistrations = false; 
                }
            });
            var bus = container.GetInstance<IBus>();
            var remote = container.GetInstance<IRemote<IAggregate>>();

            await await bus.CommandAsync(new CreateRoot("Root"));
            await await bus.CommandAsync(new CreateRoot("Root2"));

            var result = await remote.Push(BranchManager.Master);
            
            // +1 because of command log
            Assert.Equal(2 + 1, result.NumberOfStreams);
            Assert.Equal(2 * (1 + 1), result.NumberOfMessages);
        }
    }
}