using System.Threading.Tasks;
using NodaTime;
using Xunit;
using ZES.Infrastructure.Branching;
using ZES.Infrastructure.Utils;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Infrastructure;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using ZES.Utils;

namespace ZES.Tests;

public class FutureTests : ZesTest
{
    public FutureTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task CanQueueCommandInTheFuture()
    {
        var container = CreateContainer();
        
        var bus = container.GetInstance<IBus>();
        var manager = container.GetInstance<IBranchManager>();
        var timeline = container.GetInstance<IActiveTimeline>();
        
        var date = timeline.Now;
        var futureDate = date.PlusPeriod(Period.FromYears(1));
        
        var command = new CreateRoot(nameof(CanQueueCommandInTheFuture)) { Timestamp = futureDate };
        await bus.Command(command);
        var queuedCommand = timeline.DequeCommand();
        Assert.NotNull(queuedCommand);
    }
    
    [Fact]
    public async Task CanExecuteCommandInTheFuture()
    {
        var container = CreateContainer();
        
        var bus = container.GetInstance<IBus>();
        var timeline = container.GetInstance<IActiveTimeline>();
        var manager = container.GetInstance<IBranchManager>();
        
        var futureTimeline = await manager.Branch("test", timeline.Now);
        
        var date = futureTimeline.Now;
        var period = Period.FromYears(1);
        var futureDate = date.PlusPeriod(period);
        var futureDate2 = futureDate.PlusPeriod(period);
        
        await bus.Command(new CreateRoot(nameof(CanExecuteCommandInTheFuture)) { Timestamp = futureDate });
        await bus.Command(new UpdateRoot(nameof(CanExecuteCommandInTheFuture)) { Timestamp = futureDate2 });
        
        await manager.Advance(futureTimeline.Id, futureDate2);

        var stats = await bus.QueryAsync(new StatsQuery());
        Assert.Equal(1, stats.NumberOfRoots);
        
        var rootInfo = await bus.QueryAsync(new RootInfoQuery(nameof(CanExecuteCommandInTheFuture)));
        Assert.Equal(futureDate2, rootInfo.UpdatedAt);
    }

    [Fact]
    public async Task CanExecuteCommandInTheFutureOnMaster()
    {
        var container = CreateContainer();
        
        var bus = container.GetInstance<IBus>();
        var timeline = container.GetInstance<IActiveTimeline>();
        
        var now = timeline.Now;
        var futureDate = now.PlusPeriod(Period.FromSeconds(1));
        var futureDate2 = futureDate.PlusPeriod(Period.FromSeconds(1));
        
        await bus.Command(new CreateRoot(nameof(CanExecuteCommandInTheFutureOnMaster)) { Timestamp = futureDate });
        await bus.Command(new UpdateRoot(nameof(CanExecuteCommandInTheFutureOnMaster)) { Timestamp = futureDate2 });

        var stats = await bus.QueryAsync(new StatsQuery());
        Assert.Equal(0, stats.NumberOfRoots);
        
        await Task.Delay((futureDate2 - timeline.Now).ToTimeSpan(), TestContext.Current.CancellationToken);
        
        await bus.QueryUntil(new StatsQuery(), s => s.NumberOfRoots == 1);
        await bus.QueryUntil(new RootInfoQuery(nameof(CanExecuteCommandInTheFutureOnMaster)), r => r.UpdatedAt == futureDate2);
    }

    [Fact]
    public async Task CanExecuteCommandInTheFutureOnLiveBranch()
    {
        var container = CreateContainer();
        
        var bus = container.GetInstance<IBus>();
        var manager = container.GetInstance<IBranchManager>();
        var timeline = container.GetInstance<IActiveTimeline>();
        
        var branch = await manager.Branch("test");
        var now = branch.Now;

        var futureDate = now.PlusPeriod(Period.FromSeconds(1));
        var futureDate2 = futureDate.PlusPeriod(Period.FromSeconds(1));
        
        await bus.Command(new CreateRoot(nameof(CanExecuteCommandInTheFutureOnLiveBranch)) { Timestamp = futureDate });
        await bus.Command(new UpdateRoot(nameof(CanExecuteCommandInTheFutureOnLiveBranch)) { Timestamp = futureDate2 });

        var stats = await bus.QueryAsync(new StatsQuery());
        Assert.Equal(0, stats.NumberOfRoots);
        
        await Task.Delay((futureDate2 - timeline.Now).ToTimeSpan(), TestContext.Current.CancellationToken);
        
        await bus.QueryUntil(new StatsQuery(), s => s.NumberOfRoots == 1);
        await bus.QueryUntil(new RootInfoQuery(nameof(CanExecuteCommandInTheFutureOnLiveBranch)), r => r.UpdatedAt == futureDate2);

        await manager.Branch(BranchManager.Master);
        
        stats = await bus.QueryAsync(new StatsQuery());
        Assert.Equal(0, stats.NumberOfRoots);
    }
    
    [Fact]
    public async Task CanCopyPendingCommandsToLiveBranch()
    {
        var container = CreateContainer();
        
        var bus = container.GetInstance<IBus>();
        var manager = container.GetInstance<IBranchManager>();
        var timeline = container.GetInstance<IActiveTimeline>();

        var now = timeline.Now;
        var futureDate = now.PlusPeriod(Period.FromSeconds(1));
        var futureDate2 = futureDate.PlusPeriod(Period.FromSeconds(1));
        
        await bus.Command(new CreateRoot(nameof(CanCopyPendingCommandsToLiveBranch)) { Timestamp = futureDate });
        await bus.Command(new UpdateRoot(nameof(CanCopyPendingCommandsToLiveBranch)) { Timestamp = futureDate2 });

        await manager.Branch("test");
        
        var stats = await bus.QueryAsync(new StatsQuery());
        Assert.Equal(0, stats.NumberOfRoots);
        
        await Task.Delay((futureDate2 - timeline.Now).ToTimeSpan(), TestContext.Current.CancellationToken);
        
        stats = await bus.QueryAsync(new StatsQuery());
        Assert.Equal(1, stats.NumberOfRoots);
        
        await bus.QueryUntil(new RootInfoQuery(nameof(CanCopyPendingCommandsToLiveBranch)), r => r.UpdatedAt == futureDate2);

        manager.Reset();
        
        await bus.QueryUntil(new StatsQuery(), s => s.NumberOfRoots == 1);
        await bus.QueryUntil(new RootInfoQuery(nameof(CanCopyPendingCommandsToLiveBranch)), r => r.UpdatedAt == futureDate2);
    }
}