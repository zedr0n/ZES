using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.EventStore
{
    /// <summary>
    /// SQLStreamStore event store facade
    /// </summary>
    /// <typeparam name="TEventSourced">Event sourced type</typeparam>
    public class SqlEventStoreEx<TEventSourced> : EventStoreBase<TEventSourced, NewStreamMessage, StreamMessage> 
        where TEventSourced : IEventSourced
    {
        private readonly IStreamStore _streamStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlEventStoreEx{TEventSourced}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">Event serializer</param>
        /// <param name="log">Log service</param>
        /// <param name="streamStore">Stream store</param>
        public SqlEventStoreEx(IMessageQueue messageQueue, ISerializer<IEvent> serializer, ILog log, IStreamStore streamStore)
            : base(messageQueue, serializer, log)
        {
            _streamStore = streamStore;
        }

        /// <inheritdoc />
        protected override async Task ListStreamsObservable(IObserver<IStream> observer, Func<string, bool> predicate, CancellationToken token)
        {
            var page = await _streamStore.ListStreams(Pattern.Anything(), Configuration.BatchSize, default, token);
            while (page.StreamIds.Length > 0 && !token.IsCancellationRequested)
            {
                foreach (var s in page.StreamIds.Where(predicate))
                {
                    var stream = await _streamStore.GetStream(s, Serializer).Timeout();
                        
                    if (typeof(TEventSourced) == typeof(ISaga) && !stream.IsSaga) 
                        continue;
                    if (typeof(TEventSourced) == typeof(IAggregate) && stream.IsSaga)
                        continue;
                        
                    observer.OnNext(stream);
                }

                page = await page.Next(token);
            }

            observer.OnCompleted();
        }

        /// <inheritdoc />
        protected override async Task ReadSingleStreamStore<TEvent>(IObserver<TEvent> observer, IStream stream, int position, int count)
        {
            var page = await _streamStore.ReadStreamForwards(stream.Key, position, Math.Min(Configuration.BatchSize, count));
            while (page.Messages.Length > 0 && count > 0)
            {
                count = await DecodeEventsObservable(observer, page.Messages, count);
                page = await page.ReadNext();
            }
            
            observer.OnCompleted();
        }

        /// <inheritdoc/>
        protected override async Task<int> AppendToStreamStore(IStream stream, IList<NewStreamMessage> streamMessages)
        {
            var result = await _streamStore.AppendToStream(stream.Key, stream.AppendPosition(), streamMessages.ToArray());
            return result.CurrentVersion;
        }

        /// <inheritdoc />
        protected override async Task UpdateStreamMetadata(IStream stream)
        {
            /*var metaVersion = (await _streamStore.GetStreamMetadata(stream.Key)).MetadataStreamVersion;
            if (metaVersion == ExpectedVersion.EmptyStream)
                metaVersion = ExpectedVersion.NoStream;*/
            var metaVersion = ExpectedVersion.Any;
                
            await _streamStore.SetStreamMetadata(
                stream.Key,
                metaVersion, 
                metadataJson: Serializer.EncodeStreamMetadata(stream)); 
        }

        /// <inheritdoc />
        protected override async Task TruncateStreamStore(IStream stream, int version)
        {
            var events = await ReadStream<IEventMetadata>(stream, version + 1).ToList();
            foreach (var e in events.Reverse())
                await _streamStore.DeleteMessage(stream.Key, e.MessageId);
        }

        /// <inheritdoc />
        protected override async Task DeleteStreamStore(IStream stream)
        {
            await _streamStore.DeleteStream(stream.Key);
        }
    }
}