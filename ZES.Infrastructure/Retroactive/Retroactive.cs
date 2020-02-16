using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Retroactive
{
    /// <inheritdoc />
    public class Retroactive : IRetroactive
    {
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IEventStore<ISaga> _sagaStore;
        private readonly IBranchManager _manager;
        private readonly IGraph _graph;
        private readonly IStreamLocator _streamLocator;
        private readonly IMessageQueue _messageQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Retroactive"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="graph">Graph</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="streamLocator">Stream locator</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="sagaStore">Saga store</param>
        public Retroactive(IEventStore<IAggregate> eventStore, IGraph graph, IBranchManager manager, IStreamLocator streamLocator, IMessageQueue messageQueue, IEventStore<ISaga> sagaStore)
        {
            _eventStore = eventStore;
            _graph = graph;
            _manager = manager;
            _streamLocator = streamLocator;
            _messageQueue = messageQueue;
            _sagaStore = sagaStore;
        }

        /// <inheritdoc />
        public async Task InsertIntoStream(IStream stream, int version, IEnumerable<IEvent> events)
        {
            var store = GetStore(stream);
            var origVersion = version;
            var currentBranch = _manager.ActiveBranch;
            var liveStream = _streamLocator.FindBranched(stream, currentBranch);

            IList<IEvent> laterEvents = new List<IEvent>();

            if (liveStream != null)
                laterEvents = await store.ReadStream<IEvent>(liveStream, version).ToList();
            else
                stream = stream.Branch(currentBranch, ExpectedVersion.EmptyStream);

            if (laterEvents.Count == 0)
            {
                await AppendToStream(stream, version, events);
                return;
            }

            var enumerable = events.ToList();
            var time = default(long);
            if (version > 0)
                time = (await store.ReadStream<IEvent>(stream, version - 1, 1).SingleAsync()).Timestamp;
            else
                time = enumerable.First().Timestamp;

            var tempStreamId = $"{stream.Timeline}-{stream.Id}-{version}";
            var branch = await _manager.Branch(tempStreamId, time);

            var newStream = _streamLocator.FindBranched(stream, branch.Id);
            if (newStream == null)
                throw new InvalidOperationException($"Stream {tempStreamId}:{stream.Type}:{stream.Id} not found!");

            foreach (var e in enumerable)
            {
                e.Version = version;
                e.Stream = stream.Key;
                version++;
            }
            
            await store.AppendToStream(newStream, enumerable, false);

            foreach (var e in laterEvents)
            {
                e.Version = version;
                e.Stream = stream.Key;
                version++;
            }

            newStream = _streamLocator.Find(newStream);
            await store.AppendToStream(newStream, laterEvents, false);

            await _manager.Branch(currentBranch);
            await TrimStream(liveStream, origVersion - 1);
            
            // _graph.Serialise("trim");
            await _manager.Merge(tempStreamId);
            await _manager.DeleteBranch(tempStreamId);
        }

        /// <inheritdoc />
        public async Task TrimStream(IStream stream, int version)
        {
            var store = GetStore(stream);
            await store.TrimStream(stream, version);
            await _graph.TrimStream(stream.Key, version);
            _messageQueue.Alert(new OnTimelineChange());
        }
        
        private async Task AppendToStream(IStream stream, int version, IEnumerable<IEvent> events)
        {
            var currentBranch = _manager.ActiveBranch;

            var enumerable = events.ToList();
            foreach (var e in enumerable)
            {
                e.Version = version;
                e.Stream = currentBranch;
                version++;
            }

            var store = GetStore(stream);
            await store.AppendToStream(stream, enumerable, false);
        }

        private IEventStore GetStore(IStream stream)
        {
            if (stream.IsSaga)
                return _sagaStore;
            return _eventStore;
        }
    }
}