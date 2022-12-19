using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
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
        private IDisposable _subscription;
        private Task _ready;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamLocator"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="sagaStore">Saga store</param>
        /// <param name="messageQueue">Message queue</param>
        public StreamLocator(
            IEventStore<IAggregate> eventStore,
            IEventStore<ISaga> sagaStore,
            IMessageQueue messageQueue)
        {
            _eventStore = eventStore;
            _sagaStore = sagaStore;

            messageQueue.Alerts.OfType<PullCompleted>().Subscribe(e => Repopulate());
            Repopulate();
        }

        /// <inheritdoc />
        public Task Ready
        {
            get
            {
                if (_ready == null)
                    _ready = _eventStore.ListStreams().Concat(_sagaStore.ListStreams()).Select(GetOrAdd).LastOrDefaultAsync().ToTask();

                return _ready;
            }
            private set => _ready = value;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IStream>> ListStreams(string branchId)
        {
            await Ready;
            return _streams.Where(s => s.Key.StartsWith(branchId + ":")).Select(s => s.Value);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IStream>> ListStreams<T>(string branchId)
            where T : IEventSourced
        {
            await Ready;
            var isSaga = typeof(T).Name == nameof(ISaga);
            return _streams.Where(s => s.Key.StartsWith(branchId + ":") && s.Value.IsSaga == isSaga).Select(s => s.Value);
        }

        /// <inheritdoc />
        public async Task<IStream> Find<T>(string id, string timeline = "master")
            where T : IEventSourced
        {
            await Ready;
            var aStream = new Stream(id, typeof(T).Name, ExpectedVersion.NoStream, timeline);
            return _streams.TryGetValue(aStream.Key, out var stream) ? stream : default; 
        }

        /// <inheritdoc />
        public async Task<IStream> Find(IStream stream)
        {
            await Ready;
            return _streams.TryGetValue(stream.Key, out var outStream) ? outStream : default;  
        }

        /// <inheritdoc />
        public async Task<IStream> FindBranched(IStream stream, string timeline)
        {
            await Ready;
            var aStream = new Stream(stream.Id, stream.Type, ExpectedVersion.NoStream, timeline);
            return _streams.TryGetValue(aStream.Key, out var theStream) ? theStream : default;
        }

        /// <inheritdoc />
        public IStream CreateEmpty(IEventSourced es, string timeline = "") => new Stream(es, timeline);

        private IStream GetOrAdd(IStream stream)
        {
            if (stream == null || stream.Key.StartsWith("$$"))
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

        private async Task Repopulate()
        {
            _streams.Clear();
            _subscription?.Dispose();
            Ready = null;
            _subscription = _eventStore.Streams.Merge(_sagaStore.Streams).Subscribe(s => GetOrAdd(s)); 
            await Ready;
        }
    }
}