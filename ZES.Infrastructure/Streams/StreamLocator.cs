using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Streams
{
    /// <inheritdoc />
    public class StreamLocator<I> : IStreamLocator<I>
        where I : IEventSourced
    {
        private readonly ConcurrentDictionary<string, IStream> _streams = new ConcurrentDictionary<string, IStream>();
        private readonly IEventStore<I> _eventStore;
        private readonly ILog _log;
        private IDisposable _subscription;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamLocator{I}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="log">Application log</param>
        public StreamLocator(IEventStore<I> eventStore, IMessageQueue messageQueue, ILog log)
        {
            _eventStore = eventStore;
            _log = log;
            messageQueue.Alerts.OfType<OnTimelineChange>().Subscribe(e => Restart());
            Restart();
        }

        /// <inheritdoc />
        public IStream Find<T>(string id, string timeline = "master")
            where T : I
        {
            var aStream = new Stream(id, typeof(T).Name, ExpectedVersion.NoStream, timeline);
            return _streams.TryGetValue(aStream.Key, out var stream) ? stream : default(IStream);
        }

        /// <inheritdoc />
        public IStream GetOrAdd(I es, string timeline = "master")
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
            _log.Trace(string.Empty, this);
            _subscription?.Dispose();
            _subscription = _eventStore.ListStreams().Concat(_eventStore.Streams).Subscribe(stream => GetOrAdd(stream));            
        }
    }
}