using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure.Retroactive;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;

namespace ZES.Tests
{
    public class RetroactiveTests : ZesTest
    {
        public RetroactiveTests(ITestOutputHelper outputHelper) 
            : base(outputHelper)
        {
        }

        [Fact]
        public async void CanInsertIntoStream()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();
            var manager = container.GetInstance<IBranchManager>();
            var eventStore = container.GetInstance<IEventStore<IAggregate>>();
            var streamLocator = container.GetInstance<IStreamLocator<IAggregate>>();
            var retroactive = container.GetInstance<IRetroactive>();
            var graph = container.GetInstance<IQGraph>();

            await await bus.CommandAsync(new CreateRoot("Root"));
            var timestamp = timeline.Now;

            var branch = await manager.Branch("test0");
            await await bus.CommandAsync(new UpdateRoot("Root") { Timestamp = timestamp + (1000 * 60) });

            manager.Reset();
            await manager.Branch("test", timestamp);
            await await bus.CommandAsync(new UpdateRoot("Root"));
            var stream = streamLocator.Find<Root>("Root", branch.Id);

            var e = await eventStore.ReadStream<IEvent>(stream, 0).LastAsync();

            manager.Reset();
            await manager.Branch("test0");
            stream = streamLocator.Find<Root>("Root", branch.Id);
            await retroactive.InsertIntoStream(stream, 1, new[] { e });

            manager.Reset();
            await manager.Merge("test0-Root-1");
            
            await manager.DeleteBranch("test");
            await manager.DeleteBranch("test0");
            await manager.DeleteBranch("test0-Root-1");
            
            graph.Serialise(nameof(CanInsertIntoStream));
        }
    }
}