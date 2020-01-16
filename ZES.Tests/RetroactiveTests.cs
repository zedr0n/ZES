using System;
using System.Reactive.Linq;
using System.Threading;
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
using ZES.Tests.Domain.Queries;

namespace ZES.Tests
{
    public class RetroactiveTests : ZesTest
    {
        public RetroactiveTests(ITestOutputHelper outputHelper) 
            : base(outputHelper)
        {
        }

        [Fact]
        public async void CanTrimStream()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var streamLocator = container.GetInstance<IStreamLocator<IAggregate>>();
            var retroactive = container.GetInstance<IRetroactive>();

            await await bus.CommandAsync(new CreateRoot("Root"));
            await bus.IsTrue(new RootInfoQuery("Root"), c => c.CreatedAt == c.UpdatedAt);
            await await bus.CommandAsync(new UpdateRoot("Root"));
            await bus.IsTrue(new RootInfoQuery("Root"), c => c.CreatedAt < c.UpdatedAt);
            
            var stream = streamLocator.Find<Root>("Root");
            await retroactive.TrimStream(stream, 0);
            await bus.IsTrue(new RootInfoQuery("Root"), c => c.CreatedAt == c.UpdatedAt);

            stream = streamLocator.Find<Root>("Root");
            Assert.Equal(0, stream.Version);
        }

        [Fact]
        public async void CanInsertIntoStream()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();
            var manager = container.GetInstance<IBranchManager>();
            var streamLocator = container.GetInstance<IStreamLocator<IAggregate>>();
            var eventStore = container.GetInstance<IEventStore<IAggregate>>();
            var retroactive = container.GetInstance<IRetroactive>();
            var graph = container.GetInstance<IQGraph>();

            await await bus.CommandAsync(new CreateRoot("Root"));
            
            var timestamp = timeline.Now;
            var lastTime = timestamp + (60 * 1000);

            await await bus.CommandAsync(new UpdateRoot("Root", lastTime));
            await bus.Equal(new RootInfoQuery("Root"), r => r.UpdatedAt, lastTime);

            await manager.Branch("test", timestamp);
            await await bus.CommandAsync(new UpdateRoot("Root"));
            var stream = streamLocator.Find<Root>("Root", timeline.Id);

            var e = await eventStore.ReadStream<IEvent>(stream, 0).LastAsync();

            manager.Reset();
            stream = streamLocator.Find<Root>("Root", timeline.Id);
            await retroactive.InsertIntoStream(stream, 1, new[] { e });

            await bus.Equal(new RootInfoQuery("Root"), r => r.UpdatedAt, lastTime);
            graph.Serialise(nameof(CanInsertIntoStream));
        }
        
        [Fact]
        public async void CanInsertIntoStreamMultipleBranch()
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
            var lastTime = timestamp + (1000 * 60);
            branch.Warp(DateTimeOffset.FromUnixTimeMilliseconds(lastTime).DateTime);
            await await bus.CommandAsync(new UpdateRoot("Root"));

            manager.Reset();
            await manager.Branch("test", timestamp);
            await await bus.CommandAsync(new UpdateRoot("Root"));
            var stream = streamLocator.Find<Root>("Root", branch.Id);

            var e = await eventStore.ReadStream<IEvent>(stream, 0).LastAsync();

            manager.Reset();
            await manager.Branch("test0");
            stream = streamLocator.Find<Root>("Root", branch.Id);
            await retroactive.InsertIntoStream(stream, 1, new[] { e });
            
            graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch) + "-test0");

            manager.Reset();
            await manager.Merge("test0");

            graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch) + "-full");

            await manager.DeleteBranch("test");
            await manager.DeleteBranch("test0");
            
            graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch));
            
            await bus.Equal(new RootInfoQuery("Root"), r => r.UpdatedAt, lastTime);
        }
    }
}