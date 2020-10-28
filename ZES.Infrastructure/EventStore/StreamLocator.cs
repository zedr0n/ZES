using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.EventStore
{
    /// <inheritdoc />
    public class StreamLocator : IStreamLocator
    {
        private readonly ConcurrentDictionary<string, IStream> _streams = new ConcurrentDictionary<string, IStream>();
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IEventStore<ISaga> _sagaStore;
        private readonly ILog _log;
        private IDisposable _subscription;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamLocator"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="sagaStore">Saga store</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="log">Application log</param>
        public StreamLocator(
            IEventStore<IAggregate> eventStore,
            IEventStore<ISaga> sagaStore,
            IMessageQueue messageQueue,
            ILog log)
        {
            _eventStore = eventStore;
            _log = log;
            _sagaStore = sagaStore;

            // messageQueue.Alerts.OfType<OnTimelineChange>().Subscribe(e => Restart());
            messageQueue.Alerts.OfType<PullCompleted>().Subscribe(e => Restart());
            Restart();
        }

        /// <inheritdoc />
        public IStream Find(string key)
        {
            return _streams.ContainsKey(key) ? _streams[key] : null;
        }

        /// <inheritdoc />
        public IEnumerable<IStream> ListStreams(string branchId)
        {
            return _streams.Where(s => s.Key.StartsWith(branchId)).Select(s => s.Value);
        }

        /// <inheritdoc />
        public IEnumerable<IStream> ListStreams<T>(string branchId)
            where T : IEventSourced
        {
            var isSaga = typeof(T).Name == nameof(ISaga);
            return _streams.Where(s => s.Key.StartsWith(branchId) && s.Value.IsSaga == isSaga).Select(s => s.Value);
        }

        /// <inheritdoc />
        public IStream Find<T>(string id, string timeline = "master")
            where T : IEventSourced
        {
            var aStream = new Stream(id, typeof(T).Name, ExpectedVersion.NoStream, timeline);
            return _streams.TryGetValue(aStream.Key, out var stream) ? stream : default(IStream); 
        }

        /// <inheritdoc />
        public IStream Find(IStream stream)
        {
            return _streams.TryGetValue(stream.Key, out var outStream) ? outStream : stream;  
        }

        /// <inheritdoc />
        public IStream FindBranched(IStream stream, string timeline)
        {
            var aStream = new Stream(stream.Id, stream.Type, ExpectedVersion.NoStream, timeline);
            return _streams.TryGetValue(aStream.Key, out var theStream) ? theStream : default(IStream);
        }

        /// <inheritdoc />
        public IStream CreateEmpty(IEventSourced es, string timeline = "") => new Stream(es, timeline);

        private IStream GetOrAdd(IStream stream)
        {
            if (stream.Key.StartsWith("$$"))
                return null;

            if (stream.Version == ExpectedVersion.NoStream)
            {
                _streams.TryRemove(stream.Key, out _);
                return null;
            }
            
            var cStream = _streams.GetOrAdd(stream.Key, stream);
            cStream.Version = stream.Version;
            return cStream;
        }
        
        private void Restart()
        {
            _streams.Clear();
            _subscription?.Dispose();
            _subscription = _eventStore.ListStreams()
                .Concat(_sagaStore.ListStreams())
                .Concat(_eventStore.Streams)
                .Merge(_sagaStore.Streams)
                .Subscribe(stream => GetOrAdd(stream));
        }
    }
}