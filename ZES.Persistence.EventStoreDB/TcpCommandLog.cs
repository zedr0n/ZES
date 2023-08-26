using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;

namespace ZES.Persistence.EventStoreDB
{
    /// <inheritdoc />
    public class TcpCommandLog : CommandLogBase<EventData, RecordedEvent>
    {
        private readonly IEventStoreConnection _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpCommandLog"/> class.
        /// </summary>
        /// <param name="serializer">Command serializer</param>
        /// <param name="timeline">Timeline instance</param>
        /// <param name="log">Log service</param>
        /// <param name="connection">EventStore connection</param>
        public TcpCommandLog(ISerializer<ICommand> serializer, ITimeline timeline, ILog log, IEventStoreConnection connection)
            : base(serializer, timeline, log)
        {
            _connection = connection;
        }

        /// <inheritdoc />
        public override Task DeleteBranch(string branchId)
        {
            Log.Warn("Branches cannot be deleted for EventStore");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task ReadStreamStore(IObserver<ICommand> observer, IStream stream, int position, int count)
        {
            StreamEventsSlice slice;
            do
            {
                slice = await _connection.ReadStreamEventsForwardAsync(stream.Key, position, Math.Min(Configuration.BatchSize, count), false);
                position = (int)slice.NextEventNumber;

                foreach (var e in slice.Events)
                {
                    var json = Encoding.UTF8.GetString(e.Event.Data);
                    var command = Serializer.Deserialize(json);
                    observer.OnNext(command);
                    count--;
                    if (count <= 0)
                        break;
                }
            }
            while (count > 0 && !slice.IsEndOfStream);
        }

        /// <inheritdoc />
        protected override async Task<int> AppendToStream(string key, EventData message)
        {
            var result = await _connection.AppendToStreamAsync(key, ExpectedVersion.Any, message);
            return (int)result.NextExpectedVersion;
        }

        /// <inheritdoc />
        protected override EventData Encode(ICommand command) =>
            new (
                command.MessageId.Id,
                command.MessageType, 
                true, 
                Encoding.UTF8.GetBytes(Serializer.Serialize(command)),
                Encoding.UTF8.GetBytes(Serializer.EncodeMetadata(command) ?? string.Empty));
    }
}