using System;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    public class CommandLog : ICommandLog
    {
        private readonly IStreamStore _streamStore;
        private readonly ICommandSerializer _serializer;
        private readonly ITimeline _timeline;

        public CommandLog(IStreamStore streamStore, ICommandSerializer serializer, ITimeline timeline)
        {
            _streamStore = streamStore;
            _serializer = serializer;
            _timeline = timeline;
        }

        private NewStreamMessage Encode(ICommand command) => 
            new NewStreamMessage(Guid.NewGuid(),command.GetType().Name,_serializer.Serialize(command),_serializer.Metadata(command.Timestamp));

        public async Task AppendCommand(ICommand command)
        {
            var message = Encode(command);
            await _streamStore.AppendToStream($"{_timeline.Id}:Command:commands", ExpectedVersion.Any, message);
        }
    }
}