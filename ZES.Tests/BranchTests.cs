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
        public BranchTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async void CanCreateClone()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master",timeline.Id);
           
            var timeTraveller = container.GetInstance<ITimeTraveller>();
            await timeTraveller.Branch("test", timeline.Now);
                     
            Assert.Equal("test",timeline.Id);
            var root = await repository.Find<Root>("Root");
           
            Assert.Equal("Root",root.Id);
        }

        [Fact]
        public async void CanCreateEmpty()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IDomainRepository>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            await repository.Find<Root>("Root");
            var timeline = container.GetInstance<ITimeline>();
            Assert.Equal("master",timeline.Id);
           
            var timeTraveller = container.GetInstance<ITimeTraveller>();
            await timeTraveller.Branch("test", 0);
                     
            Assert.Equal("test",timeline.Id);

            var query = new StatsQuery();
            await RetryUntil(async () => (await bus.QueryAsync(query)).NumberOfRoots == 0);

            timeTraveller.Reset();
            
            Assert.Equal("master",timeline.Id);
            await RetryUntil(async () => await repository.Find<Root>("Root"));
            await RetryUntil(async () => (await bus.QueryAsync(query))?.NumberOfRoots == 1);
        }
    }
}