using Xunit;
using Xunit.Abstractions;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using static ZES.ObservableExtensions;

namespace ZES.Tests
{
    public class BranchTests : ZesTest
    {
        public BranchTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [Fact]
        public async void CanCreateClone()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master", timeline.Id);

            var now = timeline.Now;

            await await bus.CommandAsync(new UpdateRoot("Root"));
           
            var timeTraveller = container.GetInstance<IBranchManager>();
            await timeTraveller.Branch("test", now);
                     
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
    }
}