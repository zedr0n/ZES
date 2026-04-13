using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Infrastructure;

namespace ZES.Infrastructure.GraphQl;

public class GraphQlResolver : Dataflow<Tracked<ICommand>>
{
    private readonly ActionBlock<Tracked<ICommand>> _actionBlock;

    public GraphQlResolver(IBus bus, ILog log, IBranchManager manager)
        : base(Configuration.DataflowOptions)
    {
        log.Info("Starting graphql mutation resolver");
        _actionBlock = new ActionBlock<Tracked<ICommand>>(async c =>
        {
            //log.StopWatch.Clear();
            await bus.Command(c.Value, 0, true);
            await manager.Ready;
            c.Complete();
            //log.Info(log.StopWatch.Totals.ToImmutableSortedDictionary());
        }, Configuration.DataflowOptions.ToDataflowBlockOptions());

        RegisterChild(_actionBlock);
    }

    public override ITargetBlock<Tracked<ICommand>> InputBlock => _actionBlock;
}
