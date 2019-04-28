using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Serialization;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class SqlEventStore<I> : IEventStore<I> 
        where I : IEventSourced
    {
        private const int ReadSize = 100;
        
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
        /// <param name="timeline">Active timeline tracker</param>
        public SqlEventStore(IStreamStore streamStore, ISerializer<IEvent> serializer, IMessageQueue messageQueue, ILog log, ITimeline timeline)
        {
            _streamStore = streamStore;
            _serializer = serializer;
            _messageQueue = messageQueue;
            _log = log;
            _isDomainStore = typeof(I) == typeof(IAggregate);
            
            Events = Observable.Create(async (IObserver<IEvent> observer) =>
            {
                var streams = await ListStreams().ToList();
                
                var page = await _streamStore.ReadAllForwards(Position.Start, ReadSize);
                while (true)
                {
                    foreach (var m in page.Messages)
                    {
                        var thisStreams = streams.Where(s => s.Id == new Stream(m.StreamId).Id);
                        var timelineStream = thisStreams.SingleOrDefault(s => s.Timeline == timeline.Id);
                        
                        if (timelineStream == null)
                            continue;

                        if (timelineStream.Ancestors.Any(s => s.Key == m.StreamId) || timelineStream.Key == m.StreamId)
                        {
                            var payload = await m.GetJsonData();
                            var e = _serializer.Deserialize(payload);
                            if (e != null && e.Version <= timelineStream.Version )
                                observer.OnNext(e); 
                        }
                    }

                    if (page.IsEnd)
                        break;
                    
                    page = await page.ReadNext();
                }
                observer.OnCompleted();
            });
        }

        /// <inheritdoc />
        public IObservable<IStream> Streams => _streams.AsObservable();

        /// <inheritdoc />
        public IObservable<IEvent> Events { get; }
        
        /// <inheritdoc />
        public IObservable<IStream> ListStreams()
        {
            return Observable.Create(async (IObserver<IStream> observer) =>
            {
                var page = await _streamStore.ListStreams();
                while (page.StreamIds.Length > 0)
                {
                    foreach (var s in page.StreamIds.Where(x => !x.Contains("Command") && !x.StartsWith("$")))
                    {
                        var stream = await _streamStore.GetStream(s);
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
            if (count == -1)
                count = int.MaxValue;

            var allObservables = new List<IObservable<T>>(); 

            while (stream != null)
            {
                var cStream = stream;
                var cCount = count;
                var observable = Observable.Create(async (IObserver<T> observer) =>
                {
                    // _log.Trace($"{stream.Key} : from [{start}]");
                    var position = cStream.Position(start);
                    if (position <= ExpectedVersion.EmptyStream)
                    {
                        observer.OnCompleted();
                        return;
                    }

                    var page = await _streamStore.ReadStreamForwards(cStream.Key, position, ReadSize);
                    while (page.Messages.Length > 0 && cCount > 0)
                    {
                        foreach (var m in page.Messages)
                        {
                            if (typeof(T) == typeof(IEvent))
                            {
                                var payload = await m.GetJsonData();
                                var @event = _serializer.Deserialize(payload);
                                observer.OnNext((T)@event);
                            }
                            else
                            {
                                var payload = m.JsonMetadata;
                                var metadata = _serializer.DecodeMetadata(payload);
                                observer.OnNext((T)metadata);
                            }

                            cCount--;
                            if (cCount == 0)
                                break;
                        }
                        page = await page.ReadNext();
                    } 
                    observer.OnCompleted();
                });
                
                allObservables.Add(observable);

                stream = stream.Parent;

                if (stream?.Parent != null)
                    count = stream.Version - stream.Parent.Version + 1;
                else
                    count = stream?.Version + 1 ?? 0;
            }

            return allObservables.Aggregate((r, c) => r.Concat(c));
        }

        /// <inheritdoc />
        public async Task AppendToStream(IStream stream, IEnumerable<IEvent> enumerable = null)
        {
            var events = enumerable as IList<IEvent> ?? enumerable?.ToList();

            var streamMessages = events?.Select(_serializer.Encode).ToArray() ?? new NewStreamMessage[] { };
            var result = await _streamStore.AppendToStream(stream.Key, stream.Position(), streamMessages);
            LogEvents(streamMessages);

            var version = result.CurrentVersion;
            
            if (stream.Parent != null)
            {
                await _streamStore.SetStreamMetadata(
                    stream.Key,
                    metadataJson: JExtensions.JParent(stream.Parent.Key, stream.Parent.Version));
                
                // cloned stream starts with version + 1
                version += stream.Parent.Version + 1;
            }

            if (version > stream.Version || result.CurrentVersion == -1)
            {
                stream.Version = version;
                _streams.OnNext(stream); 
            }

            PublishEvents(events);    
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