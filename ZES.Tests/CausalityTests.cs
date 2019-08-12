using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SqlStreamStore.Streams;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Infrastructure.Branching;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;

namespace ZES.Tests
{
    public class CausalityTests : ZesTest
    {
        public CausalityTests(ITestOutputHelper outputHelper) 
            : base(outputHelper)
        {
        }

        [Fact]
        public async void CanCreateGraph()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var graph = container.GetInstance<ICausalityGraph>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);

            await await bus.CommandAsync(new UpdateRoot("Root"));
            
            var dependents = graph.GetDependents(command.MessageId);
            Assert.Equal(2, dependents.Count());

            var root = await repository.Find<Root>("Root");

            var causes = graph.GetCauses(root, root.Version, BranchManager.Master);
            Assert.Equal(3, causes.Count());
        }

        // [Theory]
        // [InlineData(100)]
        private async void CanCreateVGraph(int numberOfRepeats)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var log = container.GetInstance<ILog>();
            var graph = container.GetInstance<IGraph>();
            await graph.Initialize();
            var oEvents = container.GetInstance<IMessageQueue>().Messages;

            var events = new List<IEvent>();
            
            oEvents.Subscribe(e => events.Add(e));
            
            await await bus.CommandAsync(new CreateRoot("Root"));
            await await bus.CommandAsync(new UpdateRoot("Root"));

            var stream = string.Empty;
            foreach (var e in events)
            {
                graph.AddEvent(e);
                stream = e.Stream;
            }

            var repeat = 0;
            var version = await graph.GetStreamVersion(stream); 

            var stopWatch = Stopwatch.StartNew();
            while (repeat < numberOfRepeats)
            {
                await graph.GetStreamVersion(stream);
                repeat++;
            }
            
            await graph.ReadRequests.FirstAsync(r => r == 0).Timeout(Configuration.Timeout);

            log.Info($"No threading : {stopWatch.ElapsedMilliseconds}ms per {numberOfRepeats}");
            
            stopWatch = Stopwatch.StartNew();
            repeat = 0;
            while (repeat < numberOfRepeats)
            {
                var t = Task.Run(() => graph.GetStreamVersion(stream));
                repeat++;
            }

            await graph.ReadRequests.FirstAsync(r => r == 0).Timeout(Configuration.Timeout);

            log.Info($"Threading : {stopWatch.ElapsedMilliseconds}ms per {numberOfRepeats}");

            stopWatch = Stopwatch.StartNew();
            repeat = 0;
            while (repeat < numberOfRepeats)
            {
                var t = Task.Run(() => graph.GetStreamVersion(stream));
                repeat++;

                if (repeat == 10)
                {
                    var pause = Task.Run(() => graph.Pause(200));
                }
            }

            await graph.State.FirstAsync(s => s == GraphState.Updating).Timeout(Configuration.Timeout);
            await graph.State.FirstAsync(s => s == GraphState.Sleeping).Timeout(Configuration.Timeout);
            
            log.Info($"Threading with pause : {stopWatch.ElapsedMilliseconds}ms per {numberOfRepeats}");
            
            Assert.Equal(1, version);
        }
    }
}