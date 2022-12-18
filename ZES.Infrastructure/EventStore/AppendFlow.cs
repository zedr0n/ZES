using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.EventStore
{
    /// <summary>
    /// Concurrent append dataflow for event store
    /// </summary>
    /// <typeparam name="T">Event sourced type</typeparam>
    public class AppendFlow<T> : Dataflow<( IStream stream, List<IEvent> events )>
        where T : IEventSourced
    {
        private readonly ActionBlock<(IStream stream, List<IEvent> events)> _inputBlock;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppendFlow{T}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        public AppendFlow(IEventStore<T> eventStore)
            : base(Configuration.DataflowOptions)
        {
            _inputBlock = new ActionBlock<(IStream stream, List<IEvent> events)>(
                async x =>
                {
                    await eventStore.AppendToStream(x.stream, x.events, false);
                }, DataflowOptions.ToExecutionBlockOption(true));
            
            RegisterChild(_inputBlock);
        }

        /// <inheritdoc />
        public override ITargetBlock<(IStream stream, List<IEvent> events)> InputBlock => _inputBlock;
    }
}