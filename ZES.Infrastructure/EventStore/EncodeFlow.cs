using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.EventStore
{
    /// <summary>
    /// Dataflow for encoding events to persist to event store
    /// </summary>
    /// <typeparam name="T">Type of encoded event</typeparam>
    public class EncodeFlow<T> : Dataflow<IEvent, T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EncodeFlow{T}"/> class.
        /// </summary>
        /// <param name="dataflowOptions">Dataflow options</param>
        /// <param name="serializer">Event serializer</param>
        public EncodeFlow(DataflowOptions dataflowOptions, ISerializer<IEvent> serializer) 
            : base(dataflowOptions)
        {
            var block = new TransformBlock<IEvent, T>(
                e => serializer.Encode<T>(e), dataflowOptions.ToDataflowBlockOptions(true));  // dataflowOptions.ToExecutionBlockOption(true) );

            RegisterChild(block);
            InputBlock = block;
            OutputBlock = block;
        }

        /// <inheritdoc />
        public override ITargetBlock<IEvent> InputBlock { get; }

        /// <inheritdoc />
        public override ISourceBlock<T> OutputBlock { get; }
    }
}