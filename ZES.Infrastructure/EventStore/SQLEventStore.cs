// #define USE_CACHE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
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
    /// <inheritdoc />
    public class SqlEventStore<TEventSourced> : IEventStore<TEventSourced> 
        where TEventSourced : IEventSourced
    {
        private readonly IStreamStore _streamStore;
        private readonly ISerializer<IEvent> _serializer;
        private readonly Subject<IStream> _streams = new Subject<IStream>();
        private readonly IMessageQueue _messageQueue;
        private readonly ILog _log;

        private readonly bool _isDomainStore;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, long>> _versions = new ConcurrentDictionary<string, ConcurrentDictionary<int, long>>();
        private readonly ConcurrentDictionary<Guid, IEvent> _cache = new ConcurrentDictionary<Guid, IEvent>();

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
            _isDomainStore = typeof(TEventSourced) == typeof(IAggregate);
        }

        /// <inheritdoc />
        public IObservable<IStream> Streams => _streams.AsObservable().Select(s => s.Copy());

        /// <inheritdoc />
        public async Task<long> Size() => await _streamStore.ReadHeadPosition() + 1;

        /// <inheritdoc />
        public IObservable<IStream> ListStreams(string branch = null, Func<string, bool> predicate = null, CancellationToken token = default)
        {
            if (predicate == null)
                predicate = s => true;
            return Observable.Create(async (IObserver<IStream> observer) =>
            {
                var page = await _streamStore.ListStreams(branch != null ? Pattern.StartsWith(branch) : Pattern.Anything(), Configuration.BatchSize, default, token);
                while (page.StreamIds.Length > 0 && !token.IsCancellationRequested)
                {
                    foreach (var s in page.StreamIds.Where(x => predicate(x) && !x.Contains("Command") && !x.StartsWith("$")))
                    {
                        var stream = await _streamStore.GetStream(s, _serializer).Timeout();
                        
                        if (typeof(TEventSourced) == typeof(ISaga) && !stream.IsSaga) 
                            continue;
                        if (typeof(TEventSourced) == typeof(IAggregate) && stream.IsSaga)
                            continue;
                        
                        observer.OnNext(stream);
                    }

                    page = await page.Next(token);
                }

                observer.OnCompleted();
            });
        }

        /// <inheritdoc />
        public IObservable<T> ReadStream<T>(IStream stream, int start, int count = -1) 
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
        public async Task<int> GetVersion(IStream stream, long timestamp)
        {
            if (_versions.TryGetValue(stream.Key, out var versions))
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

            var streamMessages = events?.Select(_serializer.Encode).ToArray() ?? new NewStreamMessage[] { };
           
            var result = await _streamStore.AppendToStream(stream.Key, stream.AppendPosition(), streamMessages);
            LogEvents(streamMessages);

            var version = result.CurrentVersion - stream.DeletedCount;
            var snapshotVersion = stream.SnapshotVersion;
            var snapshotEvent = events?.LastOrDefault(e => e is ISnapshotEvent);
            if (snapshotEvent != default && snapshotEvent.Version > snapshotVersion)
                snapshotVersion = snapshotEvent.Version;

            if (stream.Parent != null)
                version += stream.Parent.Version + 1;

            if (version >= stream.Version || result.CurrentVersion == -1)
            {
                stream.Version = version;
                if (snapshotVersion > stream.SnapshotVersion)
                    stream.SnapshotVersion = snapshotVersion;

                var metaVersion = (await _streamStore.GetStreamMetadata(stream.Key)).MetadataStreamVersion;
                if (metaVersion == ExpectedVersion.EmptyStream)
                    metaVersion = ExpectedVersion.NoStream;
                
                await _streamStore.SetStreamMetadata(
                    stream.Key,
                    metaVersion, 
                    metadataJson: _serializer.EncodeStreamMetadata(stream)); // JExtensions.JStreamMetadata(stream));

                // await _graph.AddStreamMetadata(stream);
                var dict = _versions.GetOrAdd(stream.Key, new ConcurrentDictionary<int, long>());
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

                _streams.OnNext(stream); 
            }
            
            if (publish)
                PublishEvents(events);
                    
            // await UpdateGraph(events);
        }

        /// <inheritdoc />
        public async Task DeleteStream(IStream stream)
        {
            await _streamStore.DeleteStream(stream.Key);
            _streams.OnNext(stream.Branch(stream.Timeline, ExpectedVersion.NoStream));
        }

        /// <inheritdoc />
        public async Task TrimStream(IStream stream, int version)
        {
            var events = await ReadStream<IEvent>(stream, version + 1).ToList();
            foreach (var e in events.Reverse())
                await _streamStore.DeleteMessage(stream.Key, e.MessageId);
           
            var dict = _versions.GetOrAdd(stream.Key, new ConcurrentDictionary<int, long>());
            foreach (var e in events)
                dict.TryRemove(e.Version, out _);
            
            _log.Debug($"Deleted {events.Count} {(events.Count > 1 ? "events" : "event")} from {stream.Key}");

            stream.Version = version;
            stream.AddDeleted(events.Count);
            var meta = await _streamStore.GetStreamMetadata(stream.Key);
            await _streamStore.SetStreamMetadata(stream.Key, meta.MetadataStreamVersion, metadataJson: _serializer.EncodeStreamMetadata(stream));
        }

        private async Task ReadSingleStream<T>(IObserver<T> observer, IStream stream, int start, int count)
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
            
            var page = await _streamStore.ReadStreamForwards(stream.Key, position, Configuration.BatchSize);
            while (page.Messages.Length > 0 && count > 0)
            {
                if (typeof(T) == typeof(IEvent))
                {
                    var dataflow = new DeserializeEventFlow(Configuration.DataflowOptions, _serializer, _cache);
                    page.Messages.Take(count).ToList().ToObservable().Subscribe(dataflow.InputBlock.AsObserver());
                    var events = await dataflow.OutputBlock.AsObservable().ToList();
                    foreach (var e in events.OrderBy(e => e.Version))
                    {
                        observer.OnNext((T)e);
                        count--;
                        if (count == 0)
                            break;
                    }
                }
                else
                {
                    var dataflow = new DeserializeMetadataFlow(Configuration.DataflowOptions, _serializer);
                    page.Messages.Take(count).ToList().ToObservable().Subscribe(dataflow.InputBlock.AsObserver());
                    var metadata = await dataflow.OutputBlock.AsObservable().ToList();
                    foreach (var m in metadata.OrderBy(e => e.Version))
                    {
                        observer.OnNext((T)m);
                        count--;
                        if (count == 0)
                            break;
                    }
                }
                
                page = await page.ReadNext();
            } 
            
            observer.OnCompleted(); 
            _log.StopWatch.Stop("ReadSingleStream");
        }
        
        private void LogEvents(IEnumerable<NewStreamMessage> messages)
        {
            foreach (var m in messages) 
                _log.Debug($"IsSaga : {!_isDomainStore} \n {m.JsonData}");
        }

        private void PublishEvents(IEnumerable<IEvent> events)
        {
            if (!_isDomainStore || events == null)
                return;

            foreach (var e in events)
                _messageQueue.Event(e);
        }

        private class DeserializeEventFlow : Dataflow<StreamMessage, IEvent>
        {
            private readonly ConcurrentDictionary<Guid, IEvent> _cache;
            
            public DeserializeEventFlow(DataflowOptions dataflowOptions, ISerializer<IEvent> serializer, ConcurrentDictionary<Guid, IEvent> cache) 
                : base(dataflowOptions)
            {
                _cache = cache;
                var block = new TransformBlock<StreamMessage, IEvent>(
                    async m =>
                {
#if USE_CACHE
                    if (_cache.TryGetValue(m.MessageId, out var @event)) 
                        return @event;
                    
                    var payload = await m.GetJsonData();
                    @event = serializer.Deserialize(payload);
                    _cache.TryAdd(m.MessageId, @event);

                    return @event;
#else
                    var payload = await m.GetJsonData();
                    return serializer.Deserialize(payload);
#endif
                }, dataflowOptions.ToExecutionBlockOption(true));

                RegisterChild(block);
                InputBlock = block;
                OutputBlock = block;
            }

            public override ITargetBlock<StreamMessage> InputBlock { get; }
            public override ISourceBlock<IEvent> OutputBlock { get; }
        }
        
        private class DeserializeMetadataFlow : Dataflow<StreamMessage, IEventMetadata>
        {
            public DeserializeMetadataFlow(DataflowOptions dataflowOptions, ISerializer<IEvent> serializer) 
                : base(dataflowOptions)
            {
                var block = new TransformBlock<StreamMessage, IEventMetadata>(
                    m =>
                {
                    var payload = m.JsonMetadata;
                    var metadata = serializer.DecodeMetadata(payload);
                    return metadata;
                }, dataflowOptions.ToExecutionBlockOption(true));

                RegisterChild(block);
                InputBlock = block;
                OutputBlock = block;
            }
            
            public override ITargetBlock<StreamMessage> InputBlock { get; }
            public override ISourceBlock<IEventMetadata> OutputBlock { get; }
        }
    }
}