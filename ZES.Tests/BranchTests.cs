using System;
using Xunit;
using Xunit.Abstractions;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using static ZES.ObservableExtensions;

namespace ZES.Tests
{
    public class BranchTests : Test
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
            await bus.CommandAsync(command);

           await RetryUntil(async () => await repository.Find<Root>("Root"));
           var timeline = container.GetInstance<ITimeline>();
           Assert.Equal("master",timeline.Id);
           
           var timeTraveller = container.GetInstance<ITimeTraveller>();
           await timeTraveller.Branch("test", timeline.Now);
                     
           Assert.Equal("test",timeline.Id);
           var root = await RetryUntil(async () => await repository.Find<Root>("Root"));
           
           Assert.Equal("Root",root.Id);
        }
    }
}