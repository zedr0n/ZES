using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Streams
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
        public IStream GetOrAdd(IEventSourced es, string timeline = "master")
        {
            if (es == null)
                return default(IStream);

            var stream = new Stream(es.Id, es.GetType().Name, ExpectedVersion.NoStream, timeline);
            return _streams.GetOrAdd(stream.Key, stream);
        }

        /// <inheritdoc />
        public IStream GetOrAdd(IStream stream)
        {
            if (stream.Key.StartsWith("$$"))
                return null;
            
            var cStream = _streams.GetOrAdd(stream.Key, stream);
            cStream.Version = stream.Version;
            return cStream;
        }
        
        private void Restart()
        {
            // _log.Trace(string.Empty, this);
            _subscription?.Dispose();
            _subscription = _eventStore.ListStreams()
                .Concat(_sagaStore.ListStreams())
                .Concat(_eventStore.Streams)
                .Merge(_sagaStore.Streams)
                .Subscribe(stream => GetOrAdd(stream));
        }
    }
}