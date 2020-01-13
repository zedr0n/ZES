using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Retroactive
{
    /// <inheritdoc />
    public class Retroactive : IRetroactive
    {
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IBranchManager _manager;
        private readonly IQGraph _graph;
        private readonly IStreamLocator<IAggregate> _streamLocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="Retroactive"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="graph">Graph</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="streamLocator">Stream locator</param>
        public Retroactive(IEventStore<IAggregate> eventStore, IQGraph graph, IBranchManager manager, IStreamLocator<IAggregate> streamLocator)
        {
            _eventStore = eventStore;
            _graph = graph;
            _manager = manager;
            _streamLocator = streamLocator;
        }

        /// <inheritdoc />
        public async Task InsertIntoStream(IStream stream, int version, IEnumerable<IEvent> events)
        {
            var laterEvents = await _eventStore.ReadStream<IEvent>(stream, version).ToList();
            
            var time = _graph.GetTimestamp(stream.Key, version);
            var branch = await _manager.Branch($"{stream.Id}-{version}", time);

            var newStream = _streamLocator.FindBranched(stream, branch.Id);
            if (newStream == null)
                return;

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
                version++;
            }

            newStream = _streamLocator.Find(newStream);
            await _eventStore.AppendToStream(newStream, laterEvents);
        }
    }
}