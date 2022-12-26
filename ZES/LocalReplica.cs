using SqlStreamStore;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;
using ZES.Persistence.SQLStreamStore;

namespace ZES
{
    /// <summary>
    /// Local replica facade
    /// </summary>
    public class LocalReplica
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalReplica"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">Event serialize</param>
        /// <param name="serializerCommand">Command serializer</param>
        /// <param name="timeline">Timeline service</param>
        /// <param name="log">Log service</param>
        public LocalReplica(
            IMessageQueue messageQueue,
            ISerializer<IEvent> serializer,
            ISerializer<ICommand> serializerCommand,
            ITimeline timeline, 
            ILog log)
        {
            var streamStore = new InMemoryStreamStore();
            AggregateEventStore = new SqlEventStore<IAggregate>(messageQueue, serializer, log, streamStore);
            SagaEventStore = new SqlEventStore<ISaga>(messageQueue, serializer, log, streamStore);
            CommandLog = new SqlCommandLog(serializerCommand, timeline, log, streamStore);
        }

        /// <summary>
        /// Gets the aggregate event store
        /// </summary>
        public IEventStore<IAggregate> AggregateEventStore { get; }
        
        /// <summary>
        /// Gets the saga event store
        /// </summary>
        public IEventStore<ISaga> SagaEventStore { get; }
        
        /// <summary>
        /// Gets the command log
        /// </summary>
        public ICommandLog CommandLog { get; }
    }
}