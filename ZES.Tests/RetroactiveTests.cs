using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using NodaTime;
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
            var id = $"{nameof(CanCreateGraph)}-Root";
            
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);
            await manager.Branch("test"); 

            await await bus.CommandAsync(new UpdateRoot(id));

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
            var id = $"{nameof(CanTrimStream)}-Root";
            
            await await bus.CommandAsync(new CreateRoot(id));
            await bus.IsTrue(new RootInfoQuery(id), c => c.CreatedAt == c.UpdatedAt);
            await await bus.CommandAsync(new UpdateRoot(id));
            await bus.IsTrue(new RootInfoQuery(id), c => c.CreatedAt < c.UpdatedAt);
            
            var stream = await streamLocator.Find<Root>(id);
            await retroactive.TrimStream(stream, 0);
            messageQueue.Alert(new InvalidateProjections());
            await bus.IsTrue(new RootInfoQuery(id), c => c.CreatedAt == c.UpdatedAt);

            stream = await streamLocator.Find<Root>(id);
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
            var lastTime = timestamp + Duration.FromSeconds(60);
            var midTime = timestamp + ((lastTime - timestamp ) / 2);
            var id = $"{nameof(CanProcessRetroactiveCommand)}-Root";

            await await bus.CommandAsync(new CreateRoot(id));
            await await bus.CommandAsync(new RetroactiveCommand<UpdateRoot>(new UpdateRoot(id), lastTime));

            await await bus.CommandAsync(new RetroactiveCommand<UpdateRoot>(new UpdateRoot(id), midTime));
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
            var id = $"{nameof(CanDeleteFromStream)}-Root";
            
            await await bus.CommandAsync(new CreateRoot(id));
            await await bus.CommandAsync(new UpdateRoot(id));
            await await bus.CommandAsync(new UpdateRoot(id));

            await bus.Equal(new RootInfoQuery(id), r => r.NumberOfUpdates, 2);
            
            var stream = await locator.Find<Root>(id);
            var canDelete = await retroactive.TryDelete(stream, 3);
            Assert.False(canDelete);
            
            canDelete = await retroactive.TryDelete(stream, 1);
            
            Assert.True(canDelete);
            messageQueue.Alert(new InvalidateProjections());
            await bus.Equal(new RootInfoQuery(id), r => r.NumberOfUpdates, 1);

            canDelete = await retroactive.TryDelete(stream, 1);
            Assert.True(canDelete);
            messageQueue.Alert(new InvalidateProjections());
            await bus.Equal(new RootInfoQuery(id), r => r.NumberOfUpdates, 0);

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
            var log = container.GetInstance<ILog>();
            var id = $"{nameof(CanInsertIntoStream)}-Root";

            await await bus.CommandAsync(new CreateRoot(id));
            
            var timestamp = timeline.Now;
            var lastTime = timestamp + Duration.FromSeconds(60);

            await await bus.CommandAsync(new RetroactiveCommand<UpdateRoot>(new UpdateRoot(id), lastTime));
            await bus.Equal(new RootInfoQuery(id), r => r.UpdatedAt, lastTime);

            await manager.Branch("test", timestamp);
            await await bus.CommandAsync(new UpdateRoot(id));
            var stream = await streamLocator.Find<Root>(id, timeline.Id);

            var e = await eventStore.ReadStream<IEvent>(stream, 0).LastAsync();

            manager.Reset();
            stream = await streamLocator.Find<Root>(id, timeline.Id);

            await retroactive.TryInsertIntoStream(stream, 1, new[] { e });

            await bus.Equal(new RootInfoQuery(id), r => r.UpdatedAt, lastTime);
            await bus.Equal(new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery(id), e.Timestamp), r => r.UpdatedAt, e.Timestamp);
            
            await graph.Serialise(nameof(CanInsertIntoStream));
            log.Info(log.StopWatch.Totals);
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
            var id = $"{nameof(CanInsertIntoStreamMultipleBranch)}-Root";

            await await bus.CommandAsync(new CreateRoot(id));
            var timestamp = timeline.Now;

            var branch = await manager.Branch("test0");
            var lastTime = timestamp + Duration.FromSeconds(60); 
            branch.Warp(lastTime);
            await await bus.CommandAsync(new UpdateRoot(id));

            manager.Reset();
            await manager.Branch("test", timestamp);
            await await bus.CommandAsync(new UpdateRoot(id));
            var stream = await streamLocator.Find<Root>(id, branch.Id);

            var e = await eventStore.ReadStream<IEvent>(stream, 0).LastAsync();

            manager.Reset();
            await manager.Branch("test0");
            stream = await streamLocator.Find<Root>(id, branch.Id);
            await retroactive.TryInsertIntoStream(stream, 1, new[] { e });
            
            await graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch) + "-test0");

            manager.Reset();
            await manager.Merge("test0");

            await graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch) + "-full");

            await manager.DeleteBranch("test");
            await manager.DeleteBranch("test0");
            
            await graph.Serialise(nameof(CanInsertIntoStreamMultipleBranch));
            
            await bus.Equal(new RootInfoQuery(id), r => r.UpdatedAt, lastTime);
            await bus.Equal(new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery(id), e.Timestamp), r => r.UpdatedAt, e.Timestamp);
        }

        [Fact]
        public async void CanRetroactivelyApplySaga()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();
            var messageQueue = container.GetInstance<IMessageQueue>();
            var id = $"{nameof(CanRetroactivelyApplySaga)}-Root";
            
            var timestamp = timeline.Now;
            var lastTime = timestamp + Duration.FromSeconds(60);
            var midTime = timestamp + ((lastTime - timestamp) / 2);
 
            await await bus.CommandAsync(new CreateRoot(id));
            await bus.IsTrue(new RootInfoQuery($"{id}Copy"), info => info.CreatedAt < midTime);

            await await bus.CommandAsync(new RetroactiveCommand<UpdateRoot>(new UpdateRoot(id), lastTime));
            await bus.Equal(new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery(id), lastTime), r => r.UpdatedAt, lastTime);
            
            await await bus.CommandAsync(new RetroactiveCommand<CreateRoot>(new CreateRoot($"{id}Last"), lastTime));
            messageQueue.Alert(new InvalidateProjections());
            await bus.Equal(new RootInfoQuery($"{id}LastCopy"), r => r.CreatedAt, lastTime);

            await await bus.CommandAsync(
                new RetroactiveCommand<UpdateRoot>(new UpdateRoot($"{id}Last"), lastTime + Duration.FromSeconds(1)));
            
            await bus.Equal(new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery($"{id}Last"), lastTime + Duration.FromSeconds(1)), r => r.UpdatedAt, lastTime + Duration.FromSeconds(1));
            
            await await bus.CommandAsync(new RetroactiveCommand<CreateRoot>(new CreateRoot($"{id}Mid"), midTime));
            messageQueue.Alert(new InvalidateProjections());
            await bus.Equal(new RootInfoQuery($"{id}MidCopy"), r => r.CreatedAt, midTime);
        }
    }
}