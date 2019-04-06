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

        private readonly string _streamId;
        
        public CommandLog(IStreamStore streamStore, ICommandSerializer serializer, ITimeline timeline)
        {
            _streamStore = streamStore;
            _serializer = serializer;
            _timeline = timeline;

            _streamId = $"{_timeline.Id}:commands";
        }

        private NewStreamMessage Encode(ICommand command)
        {
            return new NewStreamMessage(Guid.NewGuid(),command.GetType().Name,_serializer.Serialize(command));
        }

        public async Task AppendCommand(ICommand command)
        {
            var message = Encode(command);
            await _streamStore.AppendToStream(_streamId, ExpectedVersion.Any, message);
        }
    }
}