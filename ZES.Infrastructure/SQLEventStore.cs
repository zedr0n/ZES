using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Sagas;
using ZES.Infrastructure.Serialization;
using ZES.Infrastructure.Streams;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class SqlEventStore<I> : IEventStore<I> 
        where I : IEventSourced
    {
        private readonly IStreamStore _streamStore;
        private readonly ISerializer<IEvent> _serializer;
        private readonly Subject<IStream> _streams = new Subject<IStream>();
        private readonly IMessageQueue _messageQueue;
        private readonly ILog _log;

        private readonly bool _isDomainStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlEventStore{I}"/> class
        /// </summary>
        /// <param name="streamStore">Stream store</param>
        /// <param name="serializer">Event serializer</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="log">Application log</param>
        public SqlEventStore(IStreamStore streamStore, ISerializer<IEvent> serializer, IMessageQueue messageQueue, ILog log)
        {
            _streamStore = streamStore;
            _serializer = serializer;
            _messageQueue = messageQueue;
            _log = log;
            _isDomainStore = typeof(I) == typeof(IAggregate);
        }

        /// <inheritdoc />
        public IObservable<IStream> Streams => _streams.AsObservable().Select(s => s.Copy());

        /// <inheritdoc />
        public IObservable<IStream> ListStreams(string branch = null)
        {
            return Observable.Create(async (IObserver<IStream> observer) =>
            {
                var page = await _streamStore.ListStreams();
                while (page.StreamIds.Length > 0)
                {
                    foreach (var s in page.StreamIds.Where(x => !x.Contains("Command") && !x.StartsWith("$")))
                    {
                        var stream = await _streamStore.GetStream(s).Timeout();
                        
                        if (typeof(I) == typeof(ISaga) && !stream.IsSaga) 
                            continue;
                        if (typeof(I) == typeof(IAggregate) && stream.IsSaga)
                            continue;
                        
                        if (stream.Timeline == branch || branch == null)
                            observer.OnNext(stream);
                    }

                    page = await page.Next();
                }

                observer.OnCompleted();
            });
        }

        /// <inheritdoc />
        public IObservable<T> ReadStream<T>(IStream stream, int start, int count = -1) 
            where T : IEventMetadata
        { 
            var allStreams = stream.Ancestors.ToList();
            allStreams.Add(stream);

            var allObservables = new List<IObservable<T>>();

            foreach (var s in allStreams)
            {
                var readCount = s.Count(start, count);
                if (readCount <= 0) 
                    continue;
                
                var cStart = start;
                count -= readCount;
                start += readCount;
                
                var observable = Observable.Create(async (IObserver<T> observer) =>
                    await ReadSingleStream(observer, s, cStart, readCount)); 
                allObservables.Add(observable);
            }

            if (allObservables.Any())
                return allObservables.Aggregate((r, c) => r.Concat(c));
            return Observable.Empty<T>();
        }

        /// <inheritdoc />
        public async Task AppendToStream(IStream stream, IEnumerable<IEvent> enumerable = null)
        {
            var events = enumerable as IList<IEvent> ?? enumerable?.ToList();

            var streamMessages = events?.Select(_serializer.Encode).ToArray() ?? new NewStreamMessage[] { };
            var result = await _streamStore.AppendToStream(stream.Key, stream.AppendPosition(), streamMessages);
            LogEvents(streamMessages);

            var version = result.CurrentVersion;
            
            if (stream.Parent != null)
                version += stream.Parent.Version + 1;
            
            if (version >= stream.Version || result.CurrentVersion == -1)
            {
                stream.Version = version;
                
                await _streamStore.SetStreamMetadata(
                    stream.Key,
                    metadataJson: JExtensions.JStreamMetadata(stream));
                
                _streams.OnNext(stream); 
            }

            PublishEvents(events);    
        }
        
        private async Task ReadSingleStream<T>(IObserver<T> observer, IStream stream, int start, int count)
        {
            var position = stream.ReadPosition(start);
            if (position <= ExpectedVersion.EmptyStream)
                position = 0;
                    
            if (count <= 0)
            {
                observer.OnCompleted();
                return;
            }

            var page = await _streamStore.ReadStreamForwards(stream.Key, position, Configuration.BatchSize);
            while (page.Messages.Length > 0 && count > 0)
            {
                foreach (var m in page.Messages)
                {
                    if (typeof(T) == typeof(IEvent))
                    {
                        var payload = await m.GetJsonData();
                        var @event = _serializer.Deserialize(payload);
                        ((Event)@event).Position = m.Position;
                        observer.OnNext((T)@event);
                    }
                    else
                    {
                        var payload = m.JsonMetadata;
                        var metadata = _serializer.DecodeMetadata(payload);
                        observer.OnNext((T)metadata);
                    }

                    count--;
                    if (count == 0)
                        break;
                }
                
                page = await page.ReadNext();
            } 
            
            observer.OnCompleted(); 
        }
        
        private void LogEvents(IEnumerable<NewStreamMessage> messages)
        {
            if (!_isDomainStore)
                return;
            
            foreach (var m in messages) 
                _log.Debug(m.JsonData);
        }

        private void PublishEvents(IEnumerable<IEvent> events)
        {
            if (!_isDomainStore || events == null)
                return;

            foreach (var e in events)
                _messageQueue.Event(e);
        }
    }
}