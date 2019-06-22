using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Domain
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
        public IObservable<ICommand> GetCommands(IStream stream)
        {
            var observable = Observable.Create(async (IObserver<ICommand> observer) =>
            {
                var page = await _streamStore.ReadStreamForwards($"{stream.Timeline}:Command:{stream.Type}", ExpectedVersion.EmptyStream + 1, Configuration.BatchSize);
                while (page.Messages.Length > 0)
                {
                    foreach (var m in page.Messages)
                    {
                        var data = await m.GetJsonData();
                        var command = _serializer.Deserialize(data);
                        if (command.Target == stream.Id)
                            observer.OnNext(command);
                    }
                    
                    page = await page.ReadNext();
                }
                observer.OnCompleted();    
            });

            return observable;
        }

        /// <inheritdoc />
        public async Task AppendCommand(ICommand command)
        {
            var message = Encode(command);
            if (Environment.GetEnvironmentVariable("LOG") != null)
                _log.Debug(message.JsonData);
            
            await _streamStore.AppendToStream($"{_timeline.Id}:Command:{command.RootType}", ExpectedVersion.Any, message);
        }

        private NewStreamMessage Encode(ICommand command) =>
            new NewStreamMessage(command.MessageId, command.GetType().Name, _serializer.Serialize(command), _serializer.EncodeMetadata(command));
    }
}