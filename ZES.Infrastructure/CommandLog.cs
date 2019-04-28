using System;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class CommandLog : ICommandLog
    {
        private readonly IStreamStore _streamStore;
        private readonly ISerializer<ICommand> _serializer;
        private readonly ITimeline _timeline;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLog"/> class.
        /// </summary>
        /// <param name="streamStore">Underlying stream store</param>
        /// <param name="serializer">Serializer</param>
        /// <param name="timeline">Current timeline tracker</param>
        /// <param name="log">Application log</param>
        public CommandLog(IStreamStore streamStore, ISerializer<ICommand> serializer, ITimeline timeline, ILog log)
        {
            _streamStore = streamStore;
            _serializer = serializer;
            _timeline = timeline;
            _log = log;
        }

        /// <inheritdoc />
        public async Task AppendCommand(ICommand command)
        {
            var message = Encode(command);
            _log.Debug(message.JsonData);
            await _streamStore.AppendToStream($"{_timeline.Id}:Command:commands", ExpectedVersion.Any, message);
        }

        private NewStreamMessage Encode(ICommand command) =>
            new NewStreamMessage(Guid.NewGuid(), command.GetType().Name, _serializer.Serialize(command), _serializer.EncodeMetadata(command));
    }
}