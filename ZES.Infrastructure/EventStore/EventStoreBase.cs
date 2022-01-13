using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Gridsum.DataflowEx;
using NodaTime;
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
    /// Event store facade 
    /// </summary>
    /// <typeparam name="TEventSourced">Event-sourced types</typeparam>
    /// <typeparam name="TNewStreamMessage">Type of the stream message for new events</typeparam>
    /// <typeparam name="TStreamMessage">Type of the stream message for existing events</typeparam>
    public abstract class EventStoreBase<TEventSourced, TNewStreamMessage, TStreamMessage> : IEventStore<TEventSourced>
        where TEventSourced : IEventSourced
    {
        private readonly ISerializer<IEvent> _serializer;
        private readonly IMessageQueue _messageQueue;
        private readonly ILog _log;

        private readonly Subject<IStream> _streams = new Subject<IStream>();

        private readonly bool _isDomainStore;
        private readonly bool _useVersionCache;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, Instant>> _versions = new ConcurrentDictionary<string, ConcurrentDictionary<int, Instant>>();
        private readonly ConcurrentBag<EncodeFlow<TNewStreamMessage>> _encodeBag = new ConcurrentBag<EncodeFlow<TNewStreamMessage>>();
        private readonly ConcurrentEventFlowBag<TStreamMessage> _deserializeBag = new ConcurrentEventFlowBag<TStreamMessage>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStoreBase{TEventSourced, TNewStreamMessage, TStreamMessage}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">Event serializer</param>
        /// <param name="log">Application log</param>
        protected EventStoreBase(IMessageQueue messageQueue, ISerializer<IEvent> serializer, ILog log)
        {
            _useVersionCache = true;
            if (Environment.GetEnvironmentVariable("USEVERSIONCACHE") == "0")
                _useVersionCache = false;

            _messageQueue = messageQueue;
            _serializer = serializer;
            _log = log;

            _isDomainStore = typeof(TEventSourced) == typeof(IAggregate);
        }

        /// <inheritdoc />
        public IObservable<IStream> Streams => _streams.AsObservable().Select(s => s.Copy());
        
        /// <summary>
        /// Gets the event serializer
        /// </summary>
        protected ISerializer<IEvent> Serializer => _serializer;

        protected ILog Log => _log;
        
        /// <inheritdoc />
        public IObservable<IStream> ListStreams(string branch = null, Func<string, bool> predicate = null, CancellationToken token = default)
        {
            bool Predicate(string streamId)
            {
                if (streamId == null)
                    return false;
                
                var b = !streamId.Contains("$") && !streamId.Contains("Command");
                if (predicate != null)
                    b &= predicate(streamId);
                if (branch != null)
                    b &= streamId.StartsWith(branch);
                return b;
            }
            
            return Observable.Create(async (IObserver<IStream> observer) => await ListStreamsObservable(observer, Predicate, token));
        }

        /// <inheritdoc />
        public IObservable<T> ReadStream<T>(IStream stream, int start, int count = -1) 
            where T : class, IEventMetadata
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
        public async Task<int> GetVersion(IStream stream, Instant timestamp)
        {
            if (_useVersionCache && _versions.TryGetValue(stream.Key, out var versions))
            {
                if (!versions.Any(v => v.Value <= timestamp))
                    return ExpectedVersion.NoStream;

                return versions.Last(v => v.Value <= timestamp).Key;
            }
            
            var metadata = await ReadStream<IEventMetadata>(stream, 0)
                .LastOrDefaultAsync(e => e.Timestamp <= timestamp);

            return metadata?.Version ?? ExpectedVersion.NoStream;
        }

        /// <inheritdoc />
        public async Task AppendToStream(IStream stream, IEnumerable<IEvent> enumerable = null, bool publish = true)
        {
            var events = enumerable as IList<IEvent> ?? enumerable?.ToList();
            var snapshotEvent = events?.LastOrDefault(e => e is ISnapshotEvent);
            var streamMessages = await EncodeEvents(events);
             
            var nextVersion = await AppendToStreamStore(stream, streamMessages);
            LogEvents(streamMessages);
            
            var version = nextVersion - stream.DeletedCount;
            var snapshotVersion = stream.SnapshotVersion;
            var snapshotTimestamp = stream.SnapshotTimestamp;
            if (snapshotEvent != default && snapshotEvent.Version > snapshotVersion)
            {
                snapshotVersion = snapshotEvent.Version;
                snapshotTimestamp = snapshotEvent.Timestamp;
            }

            if (stream.Parent != null)
                version += stream.Parent.Version + 1;

            stream.Version = version;
            if (snapshotVersion > stream.SnapshotVersion)
            {
                stream.SnapshotVersion = snapshotVersion;
                stream.SnapshotTimestamp = snapshotTimestamp;
            }

            await UpdateStreamMetadata(stream);
            
            UpdateVersionCache(stream, events);
            _streams.OnNext(stream); 
            
            if (publish)
                PublishEvents(events);
        }

        /// <inheritdoc />
        public async Task DeleteStream(IStream stream)
        {
            await DeleteStreamStore(stream);    
            _streams.OnNext(stream.Branch(stream.Timeline, ExpectedVersion.NoStream));
        }

        /// <inheritdoc />
        public async Task TrimStream(IStream stream, int version)
        {
            var events = await ReadStream<IEvent>(stream, version + 1).ToList();
            await TruncateStreamStore(stream, version);

            if (_useVersionCache)
            {
                var dict = _versions.GetOrAdd(stream.Key, new ConcurrentDictionary<int, Instant>());
                foreach (var e in events)
                    dict.TryRemove(e.Version, out _);
            }
            
            _log.Debug($"Deleted ({version + 1}..{version + events.Count}) {(events.Count > 1 ? "events" : "event")} from {stream.Key}@{stream.Version}");

            stream.Version = version;
            stream.AddDeleted(events.Count);
            UpdateStreamMetadata(stream);
        }
        
        /// <summary>
        /// Observable definition for list of all streams in the store 
        /// </summary>
        /// <param name="observer">Observer instance</param>
        /// <param name="predicate">Stream predicate</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task indicating completion of the observable</returns>
        protected abstract Task ListStreamsObservable(IObserver<IStream> observer, Func<string, bool> predicate, CancellationToken token);
        
        /// <summary>
        /// Store implementation of stream read as observable 
        /// </summary>
        /// <param name="observer">Observer instance</param>
        /// <param name="stream">Stream definition</param>
        /// <param name="position">Stream position</param>
        /// <param name="count">Number of events to read</param>
        /// <typeparam name="TEvent">Event or metadata type</typeparam>
        /// <returns>Task representing the completion of the observable</returns>
        protected abstract Task ReadSingleStreamStore<TEvent>(IObserver<TEvent> observer, IStream stream, int position, int count) 
            where TEvent : class, IEventMetadata;
        
        /// <summary>
        /// Store implementation of appending messages to the stream
        /// </summary>
        /// <param name="stream">Stream definition</param>
        /// <param name="streamMessages">Stream messages to append</param>
        /// <returns>Task representing the append operation</returns>
        protected abstract Task<int> AppendToStreamStore(IStream stream, IList<TNewStreamMessage> streamMessages);
        
        /// <summary>
        /// Store implementation of updating the stream metadata
        /// </summary>
        /// <param name="stream">Stream definition</param>
        /// <returns>Task representing the metadata update operation</returns>
        protected abstract Task UpdateStreamMetadata(IStream stream);
 
        /// <summary>
        /// Store implementation of truncating the stream 
        /// </summary>
        /// <param name="stream">Stream definition</param>
        /// <param name="version">Version to truncate from</param>
        /// <returns>Task representing the truncate operation</returns>
        protected abstract Task TruncateStreamStore(IStream stream, int version);
        
        /// <summary>
        /// Store implementation of deleting the stream
        /// </summary>
        /// <param name="stream">Stream definition</param>
        /// <returns>Task representing the delete operation</returns>
        protected abstract Task DeleteStreamStore(IStream stream);
        
        /// <summary>
        /// Gets the encoded stream metadata
        /// </summary>
        /// <param name="s">Stream definition</param>
        /// <returns>Encoded stream metadata</returns>
        protected string EncodeStreamMetadata(IStream s)
        {
            return _serializer.EncodeStreamMetadata(s);
        }
        
        /// <summary>
        /// An observable producing the decoded events ( or metadata ) 
        /// </summary>
        /// <param name="observer">Observer instance</param>
        /// <param name="streamMessages">Stream messages</param>
        /// <param name="count">Number of events to decode</param>
        /// <typeparam name="T">Event or metadata</typeparam>
        /// <returns>Task representing the observable completion</returns>
        /// <exception cref="InvalidOperationException">Throws if decode failed</exception>
        protected async Task<int> DecodeEventsObservable<T>(IObserver<T> observer, IEnumerable<TStreamMessage> streamMessages, int count)
            where T : class, IEventMetadata
        {
            _log.StopWatch.Start("ReadSingleStream.Deserialize");

            var events = new List<T>();
            if (count > 1)
            {
                var aCount = Math.Min(count, Configuration.BatchSize);

                if (!_deserializeBag.TryTake<T>(out var dataflow))
                    dataflow = new DeserializeFlow<TStreamMessage, T>(Configuration.DataflowOptions, _serializer);

                if (!await (dataflow as DeserializeFlow<TStreamMessage, T>).ProcessAsync(streamMessages, events, aCount))
                    throw new InvalidOperationException("Not all events have been processed");
                        
                _log.StopWatch.Stop("ReadSingleStream.Deserialize");
                _deserializeBag.Add(dataflow);
            }
            else
            {
                var e = await _serializer.Decode<TStreamMessage, T>(streamMessages.SingleOrDefault());
                _log.StopWatch.Stop("ReadSingleStream.Deserialize");
                events.Add(e);
            }

            foreach (var e in events.OrderBy(e => e.Version))
            {
                observer.OnNext((T)e);
                count--;
                if (count == 0)
                    break;
            }

            return count;
        }
        
        private async Task<IList<TNewStreamMessage>> EncodeEvents(ICollection<IEvent> events)
        {
            var streamMessages = new List<TNewStreamMessage>();
            if (events == null)
                return streamMessages;
            
            _log.StopWatch.Start("AppendToStream.Encode");
            if (events.Count > 1)
            {
                if (!_encodeBag.TryTake(out var encodeFlow))
                    encodeFlow = new EncodeFlow<TNewStreamMessage>(Configuration.DataflowOptions, _serializer);

                if (!await encodeFlow.ProcessAsync(events, streamMessages, events.Count)) 
                    throw new InvalidOperationException($"Encoded {streamMessages.Count} of {events.Count} events");
 
                _encodeBag.Add(encodeFlow);
            }
            else
            {
                streamMessages = new List<TNewStreamMessage> { _serializer.Encode<TNewStreamMessage>(events.Single()) };
            }
                
            _log.StopWatch.Stop("AppendToStream.Encode");
            return streamMessages;
        }

        private async Task ReadSingleStream<T>(IObserver<T> observer, IStream stream, int start, int count)
            where T : class, IEventMetadata
        {
            _log.StopWatch.Start("ReadSingleStream");
            var position = stream.ReadPosition(start);
            if (position <= ExpectedVersion.EmptyStream)
                position = 0;
                    
            if (count <= 0)
            {
                observer.OnCompleted();
                _log.StopWatch.Stop("ReadSingleStream");
                return;
            }

            await ReadSingleStreamStore(observer as IObserver<IEvent>, stream, position, count);
        }
        
        private void UpdateVersionCache(IStream stream, IList<IEvent> events)
        {
            if (!_useVersionCache)
                return;
            
            var dict = _versions.GetOrAdd(stream.Key, new ConcurrentDictionary<int, Instant>());
            foreach (var e in events ?? new List<IEvent>())
                dict[e.Version] = e.Timestamp;
            var s = stream.Parent;
            while (s != null)
            {
                if (!_versions.TryGetValue(s.Key, out var d))
                    break;
                foreach (var k in d.Keys)
                    dict[k] = d[k];
                s = s.Parent;
            }
        }
        
        private void LogEvents(IEnumerable<TNewStreamMessage> messages)
        {
            if (!Configuration.LogEnabled(nameof(LogEvents)))
                return;
            foreach (var m in messages)
            {
                var json = string.Empty;
                if (m is NewStreamMessage newStreamMessage)
                    json = newStreamMessage.JsonData;
                else if (m is EventData eventData)
                    json = Encoding.UTF8.GetString(eventData.Data);
                else
                    break;
                _log.Debug($"IsSaga : {!_isDomainStore} \n {json}");
            }
        }

        private void PublishEvents(IEnumerable<IEvent> events)
        {
            if (!_isDomainStore || events == null)
                return;

            foreach (var e in events)
                _messageQueue.Event(e);
        }

        private class ConcurrentEventFlowBag<TMessage>
        {
            private readonly ConcurrentBag<Dataflow<TMessage, IEvent>> _eventBag =
                new ConcurrentBag<Dataflow<TMessage, IEvent>>();

            private readonly ConcurrentBag<Dataflow<TMessage, IEventMetadata>> _metadataBag =
                new ConcurrentBag<Dataflow<TMessage, IEventMetadata>>();
            public bool TryTake<T>(out Dataflow<TMessage, T> result)
                where T : class, IEventMetadata
            {
                bool b;

                if (typeof(T) != typeof(IEventMetadata))
                {
                    b = _eventBag.TryTake(out var eventDataflow);
                    result = eventDataflow as Dataflow<TMessage, T>;
                }
                else
                {
                    b = _metadataBag.TryTake(out var metadataDataflow);
                    result = metadataDataflow as Dataflow<TMessage, T>;
                }

                return b;
            }

            public void Add<T>(Dataflow<TMessage, T> item)
                where T : class, IEventMetadata
            {
                if (typeof(T) != typeof(IEventMetadata))
                    _eventBag.Add(item as Dataflow<TMessage, IEvent>);
                else
                    _metadataBag.Add(item as Dataflow<TMessage, IEventMetadata>);
            }
        }
    }
}