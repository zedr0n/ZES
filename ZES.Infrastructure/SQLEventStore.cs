using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using NLog;
using SqlStreamStore;
using SqlStreamStore.Logging;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure
{
    public class SqlEventStore<I> : IEventStore<I> where I : IEventSourced
    {
        public IObservable<bool> StreamsBatched => _streamsBatched.AsObservable();
        public IObservable<IStream> Streams { get; }
        
        private readonly IStreamStore _streamStore;
        private readonly IEventSerializer _serializer;
        private readonly Subject<IStream> _streams = new Subject<IStream>();
        private readonly Subject<bool> _streamsBatched = new Subject<bool>();
        private readonly IMessageQueue _messageQueue;
        private readonly ILogger _log;

        private readonly bool _isDomainStore;

        private const int ReadSize = 100;

        public SqlEventStore(IStreamStore streamStore, IEventSerializer serializer, IMessageQueue messageQueue, ILogger log)
        {
            _streamStore = streamStore;
            _serializer = serializer;
            _messageQueue = messageQueue;
            _log = log;
            _isDomainStore = typeof(I) == typeof(IAggregate);

            Streams = Observable.Create(async (IObserver<IStream> observer) =>
            {
                var page = await _streamStore.ListStreams();
                while (page.StreamIds.Length > 0)
                {
                    foreach (var s in page.StreamIds)
                    {
                        var version = (await _streamStore.ReadStreamForwards(s, StreamVersion.End, 0))
                            .LastStreamVersion;
                        var stream = new Stream(s, version);
                        observer.OnNext(stream);
                    }

                    _streamsBatched.OnNext(false);
                    page = await page.Next();
                }

                _streamsBatched.OnNext(true);
                observer.OnCompleted();
            }).Concat(Observable.Create((IObserver<IStream> observer) =>
            {
                return _streams.Subscribe(s =>
                {
                    observer.OnNext(s);
                    _streamsBatched.OnNext(true);
                });
            }));
        }

        public async Task<IEnumerable<IEvent>> ReadStream(IStream stream, int start, int count = -1)
        {
            var events = new List<IEvent>();
            if (count == -1)
                count = int.MaxValue;
            
            var page = await _streamStore.ReadStreamForwards(stream.Key,start,ReadSize);
            while (page.Messages.Length > 0 && count > 0)
            {
                foreach (var m in page.Messages)
                {
                    var payload = await m.GetJsonData();
                    var @event = _serializer.Deserialize(payload);
                    events.Add(@event);
                    count--;
                }
                page = await page.ReadNext();
            }
            return events;
        }

        private void LogEvents(IList<IEvent> events)
        {
            foreach (var e in events)
            {
                _log.Debug(e.EventType + "( " +
                                    new DateTime(e.Timestamp).ToUniversalTime().ToString(CultureInfo.InvariantCulture) + " )");
                _log.Debug(_serializer.Serialize(e));
            }
        }

        
        public async Task AppendToStream(IStream stream, IEnumerable<IEvent> enumerable)
        {
            var events = enumerable as IList<IEvent> ?? enumerable.ToList();
            if (!events.Any())
                return;

            var streamMessages = events.Select(e => new NewStreamMessage(e.EventId, e.EventType, _serializer.Serialize(e), "")).ToArray();
            var result = await _streamStore.AppendToStream(stream.Key, stream.Version,streamMessages);

            stream.Version = result.CurrentVersion;
            _streams.OnNext(stream);

            // publish non-saga events to queue
            if (_isDomainStore)
            {
                LogEvents(events);
                
                foreach (var e in events)
                    await _messageQueue.PublishAsync(e);      
            }
        }
        
        public Task AppendCommand(ICommand command)
        {
            return Task.Run(() => true);
            //throw new NotImplementedException(); 
        }
    }
}