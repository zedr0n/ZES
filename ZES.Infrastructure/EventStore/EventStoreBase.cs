using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using NodaTime;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;
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

        private readonly Subject<IStream> _streams = new Subject<IStream>();

        private readonly bool _isDomainStore;
        private readonly bool _useVersionCache;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, Time>> _versions = new ConcurrentDictionary<string, ConcurrentDictionary<int, Time>>();
        private readonly ConcurrentBag<EncodeFlow> _encodeBag = new ConcurrentBag<EncodeFlow>();
        private readonly ConcurrentEventFlowBag<TStreamMessage> _deserializeBag = new ConcurrentEventFlowBag<TStreamMessage>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStoreBase{TEventSourced, TNewStreamMessage, TStreamMessage}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">Event serializer</param>
        /// <param name="log">Application log</param>
        protected EventStoreBase(IMessageQueue messageQueue, ISerializer<IEvent> serializer, ILog log)
        {
            _useVersionCache = Configuration.UseVersionCache;

            _messageQueue = messageQueue;
            _serializer = serializer;
            Log = log;

            _isDomainStore = typeof(TEventSourced) == typeof(IAggregate);
        }

        /// <inheritdoc />
        public IObservable<IStream> Streams => _streams.AsObservable().Select(s => s.Copy());
        
        /// <summary>
        /// Gets the event serializer
        /// </summary>
        protected ISerializer<IEvent> Serializer => _serializer;

        /// <summary>
        /// Gets the log service
        /// </summary>
        protected ILog Log { get; }

        /// <inheritdoc />
        public virtual Task ResetDatabase()
        {
            return Task.CompletedTask;
        }

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
        public IObservable<T> ReadStream<T>(IStream stream, int start, int count = -1, SerializationType serializationType = SerializationType.PayloadAndMetadata) 
            where T : class, IEvent
        { 
            var allStreams = stream.Ancestors.Reverse().ToList();
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
                    await ReadSingleStream(observer, s, cStart, readCount, serializationType)); 
                allObservables.Add(observable);
            }

            if (allObservables.Any())
                return allObservables.Aggregate((r, c) => r.Concat(c));
            return Observable.Empty<T>();
        }

        /// <inheritdoc />
        public async Task<Time> GetTimestamp(IStream stream, int version)
        {
            if (_useVersionCache && _versions.TryGetValue(stream.Key, out var versions))
            {
                if (versions.TryGetValue(version, out var timestamp))
                    return timestamp;
            }

            var metadata = await ReadStream<IEvent>(stream, version, 1, SerializationType.Metadata)
                .SingleOrDefaultAsync();

            if (metadata == default)
                throw new InvalidOperationException(
                    $"Cannot read timestamp of version {version} in stream {stream.Key}");
            return metadata?.Timestamp;
        }
        
        /// <inheritdoc />
        public async Task<int> GetVersion(IStream stream, Time timestamp)
        {
            if (_useVersionCache && _versions.TryGetValue(stream.Key, out var versions))
            {
                if (!versions.Any(v => v.Value <= timestamp))
                    return ExpectedVersion.NoStream;

                return versions.Last(v => v.Value <= timestamp).Key;
            }
            
            var metadata = await ReadStream<IEvent>(stream, 0, 1, SerializationType.Metadata)
                .LastOrDefaultAsync(e => e.Timestamp <= timestamp);

            return metadata?.Version ?? ExpectedVersion.NoStream;
        }

        /// <inheritdoc />
        public async Task<string> GetHash(IStream stream, int version = int.MaxValue)
        {
            if (version == int.MaxValue)
                version = stream.Version;
            if (version < 0)
                return string.Empty;

            var metadata = await ReadStream<IEvent>(stream, version, 1, SerializationType.Metadata).LastOrDefaultAsync();
            return metadata?.StreamHash ?? string.Empty;
        }

        /// <inheritdoc />
        public async Task AppendToStream(IStream stream, IEnumerable<IEvent> enumerable = null, bool publish = true)
        {
            var events = enumerable as IList<IEvent> ?? enumerable?.ToList();
            var snapshotEvent = events?.LastOrDefault(e => e is ISnapshotEvent);

            if (events != null)
            {
                var streamHash = await GetHash(stream);
                foreach (var e in events)
                {
                    streamHash = Hashing.Crc32(streamHash + e.MessageId);
                    e.StreamHash = streamHash;
                }
            }

            var streamMessages = await EncodeEvents(events);

            var version = await AppendToStreamStore(stream, streamMessages);
            LogEvents(streamMessages);
            
            // var version = nextVersion - stream.DeletedCount;
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
            var maxVersion = stream.Version;
            var count = maxVersion - version;
            Log.StopWatch.Start("TrimStream.TruncateStreamStore");
            await TruncateStreamStore(stream, version);
            Log.StopWatch.Stop("TrimStream.TruncateStreamStore");

            if (_useVersionCache)
            {
                var dict = _versions.GetOrAdd(stream.Key, new ConcurrentDictionary<int, Time>());
                while (maxVersion > version)
                {
                    dict.TryRemove(maxVersion, out _);
                    maxVersion--;
                }
            }
            
            Log.Debug($"Deleted ({version + 1}..{maxVersion}) {(count > 1 ? "events" : "event")} from {stream.Key}@{stream.Version}");

            stream.Version = version;
            stream.AddDeleted(count);
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
        /// <param name="serializationType">Serialization type</param>
        /// <typeparam name="TEvent">Event or metadata type</typeparam>
        /// <returns>Task representing the completion of the observable</returns>
        protected abstract Task ReadSingleStreamStore<TEvent>(IObserver<TEvent> observer, IStream stream, int position, int count, SerializationType serializationType = SerializationType.PayloadAndMetadata) 
            where TEvent : class, IEvent;
        
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
        /// <param name="serializationType">Serialization type</param>
        /// <typeparam name="T">Event or metadata</typeparam>
        /// <returns>Task representing the observable completion</returns>
        /// <exception cref="InvalidOperationException">Throws if decode failed</exception>
        protected async Task<int> DecodeEventsObservable<T>(IObserver<T> observer, IEnumerable<TStreamMessage> streamMessages, int count, SerializationType serializationType = SerializationType.PayloadAndMetadata)
            where T : class, IEvent
        {
            Log.StopWatch.Start("ReadSingleStream.Deserialize");

            var events = new List<T>();
            if (count > 1)
            {
                var aCount = Math.Min(count, Configuration.BatchSize);

                if (!_deserializeBag.TryTake<T>(serializationType, out var dataflow))
                    dataflow = new DeserializeFlow<T>(Configuration.DataflowOptions, this, serializationType);

                if (!await (dataflow as DeserializeFlow<T>).ProcessAsync(streamMessages, events, aCount))
                    throw new InvalidOperationException("Not all events have been processed");
                        
                Log.StopWatch.Stop("ReadSingleStream.Deserialize");
                _deserializeBag.Add(serializationType, dataflow);
            }
            else
            {
                var e = await StreamMessageToEvent<T>(streamMessages.SingleOrDefault(), serializationType);
                Log.StopWatch.Stop("ReadSingleStream.Deserialize");
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
        
        /// <summary>
        /// Get event json
        /// </summary>
        /// <param name="message">Event message</param>
        /// <returns>Event json string</returns>
        protected abstract string GetEventJson(TNewStreamMessage message);

        /// <summary>
        /// Convert event to stream message
        /// </summary>
        /// <param name="e">Event</param>
        /// <returns>Event stream message</returns>
        protected abstract TNewStreamMessage EventToStreamMessage(IEvent e);

        /// <summary>
        /// Convert stream message to event ( or event metadata )
        /// </summary>
        /// <param name="streamMessage">Stream message</param>
        /// <param name="serializationType">Serialization type</param>
        /// <typeparam name="T">Event or metadata type</typeparam>
        /// <returns>Event or event metadata</returns>
        protected abstract Task<T> StreamMessageToEvent<T>(TStreamMessage streamMessage, SerializationType serializationType = SerializationType.PayloadAndMetadata)
            where T : class, IEvent;

        /// <summary>
        /// Gets the equivalent expected version for the store
        /// </summary>
        /// <param name="version">Target version</param>
        /// <returns>Store version</returns>
        protected abstract int GetExpectedVersion(int version);

        private async Task<IList<TNewStreamMessage>> EncodeEvents(ICollection<IEvent> events)
        {
            var streamMessages = new List<TNewStreamMessage>();
            if (events == null)
                return streamMessages;
            
            Log.StopWatch.Start("AppendToStream.Encode");
            if (events.Count > 1)
            {
                if (!_encodeBag.TryTake(out var encodeFlow))
                    encodeFlow = new EncodeFlow(Configuration.DataflowOptions, this);

                if (!await encodeFlow.ProcessAsync(events, streamMessages, events.Count)) 
                    throw new InvalidOperationException($"Encoded {streamMessages.Count} of {events.Count} events");
 
                _encodeBag.Add(encodeFlow);
            }
            else
            {
                streamMessages = new List<TNewStreamMessage> { EventToStreamMessage(events.Single()) };
            }
                
            Log.StopWatch.Stop("AppendToStream.Encode");
            return streamMessages;
        }

        private async Task ReadSingleStream<T>(IObserver<T> observer, IStream stream, int start, int count, SerializationType serializationType)
            where T : class, IEvent
        {
            Log.StopWatch.Start(nameof(ReadSingleStream));
            var position = stream.ReadPosition(start);
            if (position <= ExpectedVersion.EmptyStream)
                position = 0;
                    
            if (count <= 0)
            {
                observer.OnCompleted();
                Log.StopWatch.Stop(nameof(ReadSingleStream));
                return;
            }

            await ReadSingleStreamStore(observer as IObserver<IEvent>, stream, position, count, serializationType);
            Log.StopWatch.Stop(nameof(ReadSingleStream));
        }
        
        private void UpdateVersionCache(IStream stream, IList<IEvent> events)
        {
            if (!_useVersionCache)
                return;
            
            var dict = _versions.GetOrAdd(stream.Key, new ConcurrentDictionary<int, Time>());
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
                var json = GetEventJson(m);
                if (json != string.Empty)
                    Log.Debug($"IsSaga : {!_isDomainStore} \n {json}");
            }
        }
        
        private void PublishEvents(IEnumerable<IEvent> events)
        {
            if (!_isDomainStore || events == null || _messageQueue == null)
                return;

            foreach (var e in events)
                _messageQueue.Event(e);
        }

        private class ConcurrentEventFlowBag<TMessage>
        {
            private readonly ConcurrentDictionary<SerializationType, ConcurrentBag<Dataflow<TMessage, IEvent>>> _bagDictionary = new();

            public bool TryTake<T>(SerializationType serializationType ,out Dataflow<TMessage, T> result)
                where T : class, IEvent
            {
                var bag = _bagDictionary.GetOrAdd(serializationType, s => new ConcurrentBag<Dataflow<TMessage, IEvent>>());
                var b = bag.TryTake(out var eventDataflow);
                result = eventDataflow as Dataflow<TMessage, T>;

                return b;
            }

            public void Add<T>(SerializationType serializationType, Dataflow<TMessage, T> item)
                where T : class, IEvent
            {
                var bag = _bagDictionary.GetOrAdd(serializationType, s => new ConcurrentBag<Dataflow<TMessage, IEvent>>());
                bag.Add(item as Dataflow<TMessage, IEvent>);
            }
        }
        
        /// <summary>
        /// Dataflow for encoding events to persist to event store
        /// </summary>
        private class EncodeFlow : Dataflow<IEvent, TNewStreamMessage>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="EncodeFlow"/> class.
            /// </summary>
            /// <param name="dataflowOptions">Dataflow options</param>
            /// <param name="eventStore">Event store</param>
            public EncodeFlow(DataflowOptions dataflowOptions, EventStoreBase<TEventSourced, TNewStreamMessage, TStreamMessage> eventStore)
                : base(dataflowOptions)
            {
                var block = new TransformBlock<IEvent, TNewStreamMessage>(
                    e => eventStore.EventToStreamMessage(e), 
                    dataflowOptions.ToDataflowBlockOptions(true));  // dataflowOptions.ToExecutionBlockOption(true) );

                RegisterChild(block);
                InputBlock = block;
                OutputBlock = block;
            }

            /// <inheritdoc />
            public override ITargetBlock<IEvent> InputBlock { get; }

            /// <inheritdoc />
            public override ISourceBlock<TNewStreamMessage> OutputBlock { get; }
        }

        /// <summary>
        /// Event deserializer TPL dataflow
        /// </summary>
        /// <typeparam name="TEvent">IEvent or metadata</typeparam>
        private class DeserializeFlow<TEvent> : Dataflow<TStreamMessage, TEvent>
            where TEvent : class, IEvent
        {
            public SerializationType SerializationType { get; }
            
            /// <summary>
            /// Initializes a new instance of the <see cref="DeserializeFlow{TEvent}"/> class.
            /// </summary>
            /// <param name="dataflowOptions">Dataflow options</param>
            /// <param name="eventStore">Event store</param>
            /// <param name="serializationType">Serialization type</param>
            public DeserializeFlow(DataflowOptions dataflowOptions, EventStoreBase<TEventSourced, TNewStreamMessage, TStreamMessage> eventStore, SerializationType serializationType = SerializationType.PayloadAndMetadata) 
                : base(dataflowOptions)
            {
                SerializationType = serializationType;
                TransformBlock<TStreamMessage, TEvent> block = null;
                block = new TransformBlock<TStreamMessage, TEvent>(
                    async m => await eventStore.StreamMessageToEvent<TEvent>(m, serializationType),
                    dataflowOptions.ToDataflowBlockOptions(true)); // dataflowOptions.ToExecutionBlockOption(true));

                RegisterChild(block);
                InputBlock = block;
                OutputBlock = block;
            }

            /// <inheritdoc />
            public override ITargetBlock<TStreamMessage> InputBlock { get; }

            /// <inheritdoc />
            public override ISourceBlock<TEvent> OutputBlock { get; }
        }
    }
}