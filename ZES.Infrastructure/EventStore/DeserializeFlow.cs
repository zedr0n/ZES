using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.EventStore
{
    /// <summary>
    /// Event deserializer TPL dataflow
    /// </summary>
    /// <typeparam name="TStreamMessage">Type of stream message</typeparam>
    /// <typeparam name="TEvent">IEvent or metadata</typeparam>
    public class DeserializeFlow<TStreamMessage, TEvent> : Dataflow<TStreamMessage, TEvent>
        where TEvent : class, IEventMetadata
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeserializeFlow{TStreamMessage, TEvent}"/> class.
        /// </summary>
        /// <param name="dataflowOptions">Dataflow options</param>
        /// <param name="serializer">Event deserializer</param>
        public DeserializeFlow(DataflowOptions dataflowOptions, ISerializer<IEvent> serializer) 
            : base(dataflowOptions)
        {
            TransformBlock<TStreamMessage, TEvent> block = null;
            block = new TransformBlock<TStreamMessage, TEvent>(
                async m => await serializer.Decode<TStreamMessage, TEvent>(m),
                dataflowOptions.ToDataflowBlockOptions(true)); // dataflowOptions.ToExecutionBlockOption(true));

            RegisterChild(block);
            InputBlock = block;
            OutputBlock = block;
        }

        /// <inheritdoc />
        public override ITargetBlock<TStreamMessage> InputBlock { get; }

        /// <inheritdoc />
        public override ISourceBlock<TEvent> OutputBlock { get; }
    }
}