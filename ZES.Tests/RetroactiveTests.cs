using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure.Alerts;
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

namespace ZES.Tests
{
    public class RetroactiveTests : ZesTest
    {
        public RetroactiveTests(ITestOutputHelper outputHelper) 
            : base(outputHelper)
        {
        }

        [Fact]
        public async void CanCreateGraph()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var graph = container.GetInstance<IGraph>();
            var manager = container.GetInstance<IBranchManager>();

            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);
            await manager.Branch("test"); 

            await await bus.CommandAsync(new UpdateRoot("Root"));

            await graph.Populate();
        }

        [Fact]
        public async void CanTrimStream()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var streamLocator = container.GetInstance<IStreamLocator>();
            var retroactive = container.GetInstance<IRetroactive>();
            var messageQueue = container.GetInstance<IMessageQueue>();
            
            await await bus.CommandAsync(new CreateRoot("Root"));
            await bus.IsTrue(new RootInfoQuery("Root"), c => c.CreatedAt == c.UpdatedAt);
            await await bus.CommandAsync(new UpdateRoot("Root"));
            await bus.IsTrue(new RootInfoQuery("Root"), c => c.CreatedAt < c.UpdatedAt);
            
            var stream = await streamLocator.Find<Root>("Root");
            await retroactive.TrimStream(stream, 0);
            messageQueue.Alert(new InvalidateProjections());
            await bus.IsTrue(new RootInfoQuery("Root"), c => c.CreatedAt == c.UpdatedAt);

            stream = await streamLocator.Find<Root>("Root");
            Assert.Equal(0, stream.Version);
        }

        [Fact]
        public async void CanProcessRetroactiveCommand()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();
            var graph = container.GetInstance<IGraph>();
            
            var timestamp = timeline.Now;
            var lastTime = timestamp + (60 * 1000);
            var midTime = (timestamp + lastTime) / 2;

            await await bus.CommandAsync(new CreateRoot("Root"));
            await await bus.CommandAsync(new RetroactiveCommand<UpdateRoot>(new UpdateRoot("Root"), lastTime));

            var task = await bus.CommandAsync(new RetroactiveCommand<UpdateRoot>(new UpdateRoot("Root"), midTime));
            await task;
            await graph.Serialise(nameof(CanProcessRetroactiveCommand));
        }

        [Fact]
        public async void CanDeleteFromStream()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var retroactive = container.GetInstance<IRetroactive>();
            var locator = container.GetInstance<IStreamLocator>();
            var messageQueue = container.GetInstance<IMessageQueue>();
            
            await await bus.CommandAsync(new CreateRoot("Root"));
            await await bus.CommandAsync(new UpdateRoot("Root"));
            await await bus.CommandAsync(new UpdateRoot("Root"));

            await bus.Equal(new RootInfoQuery("Root"), r => r.NumberOfUpdates, 2);
            
            var stream = await locator.Find<Root>("Root");
            var canDelete = await retroactive.TryDelete(stream, 3);
            Assert.False(canDelete);
            
            canDelete = await retroactive.TryDelete(stream, 1);
            
            Assert.True(canDelete);
            messageQueue.Alert(new InvalidateProjections());
            await bus.Equal(new RootInfoQuery("Root"), r => r.NumberOfUpdates, 1);

            canDelete = await retroactive.TryDelete(stream, 1);
            Assert.True(canDelete);
            messageQueue.Alert(new InvalidateProjections());
            await bus.Equal(new RootInfoQuery("Root"), r => r.NumberOfUpdates, 0);

            canDelete = await retroactive.TryDelete(stream, 0);
            Assert.False(canDelete);
        }

        [Fact]
        public async void CanInsertIntoStream()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();
            var manager = container.GetInstance<IBranchManager>();
            var streamLocator = container.GetInstance<IStreamLocator>();
            var eventStore = container.GetInstance<IEventStore<IAggregate>>();
            var retroactive = container.GetInstance<IRetroactive>();
            var graph = container.GetInstance<IGraph>();

            await await bus.CommandAsync(new CreateRoot("Root"));
            
            var timestamp = timeline.Now;
            var lastTime = timestamp + (60 * 1000);

            await await bus.CommandAsync(new RetroactiveCommand<UpdateRoot>(new UpdateRoot("Root"), lastTime));
            await bus.Equal(new RootInfoQuery("Root"), r => r.UpdatedAt, lastTime);

            await manager.Branch("test", timestamp);
            await await bus.CommandAsync(new UpdateRoot("Root"));
            var stream = await streamLocator.Find<Root>("Root", timeline.Id);

            var e = await eventStore.ReadStream<IEvent>(stream, 0).LastAsync();

            manager.Reset();
            stream = await streamLocator.Find<Root>("Root", timeline.Id);

            await retroactive.TryInsertIntoStream(stream, 1, new[] { e });

            await bus.Equal(new RootInfoQuery("Root"), r => r.UpdatedAt, lastTime);
            await bus.Equal(new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery("Root"), e.Timestamp), r => r.UpdatedAt, e.Timestamp);
            
            await graph.Serialise(nameof(CanInsertIntoStream));
        }
        
        [Fact]
        public async void CanInsertIntoStreamMultipleBranch()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();
            var manager = container.GetInstance<IBranchManager>();
            var eventStore = container.GetInstance<IEventStore<IAggregate>>();
            var streamLocator = container.GetInstance<IStreamLocator>();
            var retroactive = container.GetInstance<IRetroactive>();
            var graph = container.GetInstance<IGraph>();

            await await bus.CommandAsync(new CreateRoot("Root"));
            var timestamp = timeline.Now;

            var branch = await manager.Branch("test0");
            var lastTime = timestamp + (1000 * 60);
            branch.Warp(lastTime);
            await await bus.CommandAsync(new UpdateRoot("Root"));

            manager.Reset();
            await manager.Branch("test", timestamp);
            await await bus.CommandAsync(new UpdateRoot("Root"));
            var stream = await streamLocator.Find<Root>("Root", branch.Id);

            var e = await eventStore.ReadStream<IEvent>(stream, 0).LastAsync();

            manager.Reset();
            await manager.Branch("test0");
            stream = await streamLocator.Find<Root>("Root", branch.Id);
            await retroactive.TryInsertIntoStream(stream, 1, new[] { e });
            
            await graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch) + "-test0");

            manager.Reset();
            await manager.Merge("test0");

            await graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch) + "-full");

            await manager.DeleteBranch("test");
            await manager.DeleteBranch("test0");
            
            await graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch));
            
            await bus.Equal(new RootInfoQuery("Root"), r => r.UpdatedAt, lastTime);
            await bus.Equal(new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery("Root"), e.Timestamp), r => r.UpdatedAt, e.Timestamp);
        }

        [Fact]
        public async void CanRetroactivelyApplySaga()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();
            var messageQueue = container.GetInstance<IMessageQueue>();
            
            var timestamp = timeline.Now;
            var lastTime = timestamp + (60 * 1000);
            var midTime = (timestamp + lastTime) / 2;
 
            await await bus.CommandAsync(new CreateRoot("Root"));
            await bus.IsTrue(new RootInfoQuery("RootCopy"), info => info.CreatedAt < midTime);

            await await bus.CommandAsync(new RetroactiveCommand<UpdateRoot>(new UpdateRoot("Root"), lastTime));
            await bus.Equal(new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery("Root"), lastTime), r => r.UpdatedAt, lastTime);
            
            await await bus.CommandAsync(new RetroactiveCommand<CreateRoot>(new CreateRoot("LastRoot"), lastTime));
            messageQueue.Alert(new InvalidateProjections());
            await bus.Equal(new RootInfoQuery("LastRootCopy"), r => r.CreatedAt, lastTime);

            await await bus.CommandAsync(
                new RetroactiveCommand<UpdateRoot>(new UpdateRoot("LastRoot"), lastTime + 1000));
            
            await bus.Equal(new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery("LastRoot"), lastTime + 1000), r => r.UpdatedAt, lastTime + 1000);
            
            await await bus.CommandAsync(new RetroactiveCommand<CreateRoot>(new CreateRoot("MidRoot"), midTime));
            messageQueue.Alert(new InvalidateProjections());
            await bus.Equal(new RootInfoQuery("MidRootCopy"), r => r.CreatedAt, midTime);
        }
    }
}