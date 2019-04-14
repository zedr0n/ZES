using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    public class SqlEventStore<I> : IEventStore<I> where I : IEventSourced
    {
        public IObservable<IStream> AllStreams { get; }
        public IObservable<IStream> Streams => _streams.AsObservable();
        public IObservable<IEvent> Events { get; }

        private readonly IStreamStore _streamStore;
        private readonly IEventSerializer _serializer;
        private readonly Subject<IStream> _streams = new Subject<IStream>();
        private readonly IMessageQueue _messageQueue;
        private readonly ILog _log;

        private readonly bool _isDomainStore;

        private const int ReadSize = 100;

        public SqlEventStore(IStreamStore streamStore, IEventSerializer serializer, IMessageQueue messageQueue, ILog log, ITimeline timeline)
        {
            _streamStore = streamStore;
            _serializer = serializer;
            _messageQueue = messageQueue;
            _log = log;
            _isDomainStore = typeof(I) == typeof(IAggregate);
            
            Events = Observable.Create(async (IObserver<IEvent> observer) =>
            {
                var page = await _streamStore.ReadAllForwards(Position.Start, ReadSize);
                while (true)
                {
                    foreach(var m in page.Messages)
                    {
                        var stream = new Stream(m.StreamId);
                        if (stream.Timeline != timeline.Id) 
                            continue;
                        var payload = await m.GetJsonData();
                        var e = _serializer.Deserialize(payload);
                        if(e != null)
                            observer.OnNext(e);
                    }

                    if (page.IsEnd)
                        break;
                    
                    page = await page.ReadNext();
                }
                observer.OnCompleted();
            });

            AllStreams = Observable.Create(async (IObserver<IStream> observer) =>
            {
                var page = await _streamStore.ListStreams();
                while (page.StreamIds.Length > 0)
                {
                    foreach (var s in page.StreamIds.Where(x => !x.Contains("Command")))
                    {
                        var version = (await _streamStore.ReadStreamBackwards(s, StreamVersion.End, 1))
                            .LastStreamVersion;
                        var stream = new Stream(s, version);
                        observer.OnNext(stream);
                    }

                    page = await page.Next();
                }

                observer.OnCompleted();
            }).Concat(_streams.AsObservable());
        }

        public IObservable<IEvent> ReadStream(IStream stream, int start, int count = -1)
        {
            //_log.Trace("",this);
            if (count == -1)
                count = int.MaxValue;

            var observable = Observable.Create(async (IObserver<IEvent> observer) =>
            {
                var page = await _streamStore.ReadStreamForwards(stream.Key,start,ReadSize);
                while (page.Messages.Length > 0 && count > 0)
                {
                    foreach (var m in page.Messages)
                    {
                        var payload = await m.GetJsonData();
                        var @event = _serializer.Deserialize(payload);
                        observer.OnNext(@event);
                        count--;
                        if (count == 0)
                            break;
                    }
                    page = await page.ReadNext();
                } 
                observer.OnCompleted();
            });
            
            return observable;
        }

        private void LogEvents(IEnumerable<NewStreamMessage> messages)
        {
            if (!_isDomainStore)
                return;
            
            foreach (var m in messages) 
            {
                //await _streamStore.AppendToStream("::DomainLog", ExpectedVersion.Any, m);
                _log.Debug(m.JsonData);
            }
        }

        private void PublishEvents(IEnumerable<IEvent> events)
        {
            if (!_isDomainStore)
                return;

            foreach (var e in events)
                _messageQueue.Event(e);
        }

        public async Task AppendToStream(IStream stream, IEnumerable<IEvent> enumerable)
        {
            //_log.Trace("",this);
            var events = enumerable as IList<IEvent> ?? enumerable.ToList();
            if (!events.Any())
                return;

            var streamMessages = events.Select(_serializer.Encode).ToArray();
            var result = await _streamStore.AppendToStream(stream.Key, stream.Version,streamMessages);
            LogEvents(streamMessages);

            stream.Version = result.CurrentVersion;
            _streams.OnNext(stream);

            PublishEvents(events);    
        }
    }
}