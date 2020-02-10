using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Retroactive
{
    /// <inheritdoc />
    public class Retroactive : IRetroactive
    {
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IBranchManager _manager;
        private readonly IQGraph _graph;
        private readonly IStreamLocator<IAggregate> _streamLocator;
        private readonly IMessageQueue _messageQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Retroactive"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="graph">Graph</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="streamLocator">Stream locator</param>
        /// <param name="messageQueue">Message queue</param>
        public Retroactive(IEventStore<IAggregate> eventStore, IQGraph graph, IBranchManager manager, IStreamLocator<IAggregate> streamLocator, IMessageQueue messageQueue)
        {
            _eventStore = eventStore;
            _graph = graph;
            _manager = manager;
            _streamLocator = streamLocator;
            _messageQueue = messageQueue;
        }

        /// <inheritdoc />
        public async Task InsertIntoStream(IStream stream, int version, IEnumerable<IEvent> events)
        {
            var origVersion = version;
            var currentBranch = _manager.ActiveBranch;
            var laterEvents = await _eventStore.ReadStream<IEvent>(stream, version).ToList();
            
            var time = _graph.GetTimestamp(stream.Key, version);
            if (time == default(long))
                throw new InvalidOperationException($"Version {version} not found in {stream.Key}");
            
            var tempStreamId = $"{stream.Timeline}-{stream.Id}-{version}";
            var branch = await _manager.Branch(tempStreamId, time - 1);

            var newStream = _streamLocator.FindBranched(stream, branch.Id);
            if (newStream == null)
                throw new InvalidOperationException($"Stream {tempStreamId}:{stream.Type}:{stream.Id} not found!");

            var enumerable = events.ToList();
            foreach (var e in enumerable)
            {
                e.Version = version;
                version++;
            }
            
            await _eventStore.AppendToStream(newStream, enumerable);

            foreach (var e in laterEvents)
            {
                e.Version = version;
                e.Stream = stream.Key;
                version++;
            }

            newStream = _streamLocator.Find(newStream);
            await _eventStore.AppendToStream(newStream, laterEvents);

            await _manager.Branch(currentBranch);
            await TrimStream(stream, origVersion - 1);
            
            // _graph.Serialise("trim");
            await _manager.Merge(tempStreamId);
            await _manager.DeleteBranch(tempStreamId);
        }

        /// <inheritdoc />
        public async Task TrimStream(IStream stream, int version)
        {
            await _eventStore.TrimStream(stream, version);
            await _graph.TrimStream(stream.Key, version);
            _messageQueue.Alert(new OnTimelineChange());
        }
    }
}