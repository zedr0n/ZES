using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Infrastructure;

namespace ZES.Infrastructure.GraphQl;

/// <summary>
/// Represents a GraphQL mutation resolver responsible for processing commands asynchronously
/// and coordinating state readiness within the application using an ActionBlock pipeline.
/// </summary>
/// <remarks>
/// The <see cref="GraphQlResolver"/> class integrates with a message bus and branch manager
/// for command processing and ensures that dependent state is marked as ready after a command
/// is executed. It leverages the Dataflow library to manage data processing workflows.
/// </remarks>
public class GraphQlResolver : Dataflow<Tracked<ICommand>>
{
    private readonly ActionBlock<Tracked<ICommand>> _actionBlock;

    /// <summary>
    /// Represents a GraphQL mutation resolver responsible for processing commands in a dataflow pipeline.
    /// </summary>
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

    /// <inheritdoc />
    public override ITargetBlock<Tracked<ICommand>> InputBlock => _actionBlock;
}
