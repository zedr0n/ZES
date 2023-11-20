using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;

namespace ZES.Persistence.SQLStreamStore
{
    /// <summary>
    /// SQLStreamStore implementation of command log
    /// </summary>
    public class SqlCommandLog : CommandLogBase<NewStreamMessage, StreamMessage>
    {
        private readonly IStreamStore _streamStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlCommandLog"/> class.
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="timeline">Timeline instance</param>
        /// <param name="log">Log service</param>
        /// <param name="streamStore">SQLStreamStore instance</param>
        public SqlCommandLog(ISerializer<ICommand> serializer, ITimeline timeline, ILog log, IStreamStore streamStore)
            : base(serializer, timeline, log)
        {
            _streamStore = streamStore;
        }

        /// <inheritdoc />
        public override async Task DeleteBranch(string branchId)
        {
            var page = await _streamStore.ListStreams(Configuration.BatchSize);
            while (page.StreamIds.Length > 0)
            {
                foreach (var s in page.StreamIds)
                {
                    if (!s.StartsWith(branchId) || !s.Contains("Command"))
                        continue;

                    await _streamStore.DeleteStream(s);
                    Log.Debug($"Deleted {nameof(ICommand)} stream {s}");
                }
                
                page = await page.Next();
            }
        }

        /// <inheritdoc />
        protected override async Task<List<string>> ListStreamsStore()
        {
            var streams = new List<string>();
            var page = await _streamStore.ListStreams(Pattern.Anything(), Configuration.BatchSize);
            while (page.StreamIds.Length > 0)
            {
                streams.AddRange(page.StreamIds.Where(p => p.Contains(":Command:") && !p.StartsWith("$$")));
                page = await page.Next();
            }

            return streams;
        }

        /// <inheritdoc />
        protected override async Task ReadStreamStore(IObserver<ICommand> observer, IStream stream, int position, int count)
        {
            var c = 0;
            var page = await _streamStore.ReadStreamForwards(stream.Key, position, Configuration.BatchSize);
            while (page.Messages.Length > 0)
            {
                foreach (var m in page.Messages)
                {
                    var data = await m.GetJsonData();
                    var command = Serializer.Deserialize(data);
                    observer.OnNext(command);
                    c++;
                    if (count > 0 && c >= count)
                        break;
                }

                if (count > 0 && c >= count)
                    break;
                    
                page = await page.ReadNext();
            }
                
            observer.OnCompleted();    
        }

        /// <inheritdoc />
        protected override async Task<int> AppendToStream(string key, NewStreamMessage message)
        {
            var appendResult = await _streamStore.AppendToStream(key, ExpectedVersion.Any, message);
            return appendResult.CurrentVersion;
        }

        /// <inheritdoc />
        protected override NewStreamMessage Encode(ICommand command) =>
           new (command.MessageId.Id, command.MessageType, Serializer.Serialize(command), Serializer.EncodeMetadata(command));
    }
}