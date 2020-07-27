using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.EventStore;
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
        public async Task<ICommand> GetCommand(IEvent e)
        {
            var stream = new Stream(Key(e.MessageType), ExpectedVersion.Any); 
            var obs = GetCommands(stream);

            var command = await obs.FirstOrDefaultAsync(c => e.CommandId == c.MessageId).Timeout(Configuration.Timeout);
            return command;
        }

        /// <inheritdoc />
        public async Task AppendCommand(ICommand command)
        {
            var message = Encode(command);
            _log.Debug(message.JsonData);
            
            await _streamStore.AppendToStream(Key(command.EventType), ExpectedVersion.Any, message);
        }

        /// <inheritdoc />
        public async Task DeleteBranch(string branchId)
        {
            var page = await _streamStore.ListStreams(Configuration.BatchSize);
            while (page.StreamIds.Length > 0)
            {
                foreach (var s in page.StreamIds)
                {
                    if (!s.StartsWith(branchId) || !s.Contains("Command"))
                        continue;

                    await _streamStore.DeleteStream(s);
                    _log.Trace($"Deleted {nameof(ICommand)} stream {s}");
                }
                
                page = await page.Next();
            }
        }
        
        private IObservable<ICommand> GetCommands(IStream stream)
        {
            var observable = Observable.Create(async (IObserver<ICommand> observer) =>
            {
                var page = await _streamStore.ReadStreamForwards(stream.Key, ExpectedVersion.EmptyStream + 1, Configuration.BatchSize);
                while (page.Messages.Length > 0)
                {
                    foreach (var m in page.Messages)
                    {
                        var data = await m.GetJsonData();
                        var command = _serializer.Deserialize(data);
                        observer.OnNext(command);
                    }
                    
                    page = await page.ReadNext();
                }
                
                observer.OnCompleted();    
            });

            return observable;
        }

        private string Key(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
                throw new InvalidOperationException("Event type not known for commmand");
            return $"{_timeline.Id}:Command:{eventType}";
        }
        
        private NewStreamMessage Encode(ICommand command) =>
            new NewStreamMessage(command.MessageId, command.GetType().Name, _serializer.Serialize(command), _serializer.EncodeMetadata(command));
    }
}