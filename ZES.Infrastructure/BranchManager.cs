using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using Newtonsoft.Json.Linq;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Serialization;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class BranchManager : IBranchManager
    {
        private readonly ILog _log;
        private readonly ConcurrentDictionary<string, Timeline> _branches = new ConcurrentDictionary<string, Timeline>();
        private readonly Timeline _activeTimeline;
        private readonly IMessageQueue _messageQueue;
        private readonly IEventStore<IAggregate> _eventStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="BranchManager"/> class.
        /// </summary>
        /// <param name="log">Application logger</param>
        /// <param name="activeTimeline">Root timeline</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="eventStore">Event store</param>
        public BranchManager(ILog log, ITimeline activeTimeline, IMessageQueue messageQueue, IEventStore<IAggregate> eventStore)
        {
            _log = log;
            _activeTimeline = activeTimeline as Timeline;
            _messageQueue = messageQueue;
            _eventStore = eventStore;
        }

        /// <summary>
        /// Gets root timeline id
        /// </summary>
        /// <value>
        /// Root timeline id
        /// </value>
        public static string Master { get; } = "master";

        /// <inheritdoc />
        public async Task<ITimeline> Branch(string branchId, long time)
        {
            var timeline = _branches.GetOrAdd(branchId, b => new Timeline { Id = branchId, Now = time });
            if (timeline.Now != time)
            {
                _log.Error($"Branch ${branchId} already exists!");
                throw new InvalidOperationException();    
            }
            
            // copy the events
            await Clone(branchId, time);
            
            // update current timeline
            _activeTimeline.Id = timeline.Id;
            _activeTimeline.Now = time;
            
            // refresh the stream locator
            _messageQueue.Alert(new Alerts.OnTimelineChange());
            
            // rebuild all projections
            _messageQueue.Alert(new Alerts.InvalidateProjections());
                
            return _activeTimeline;
        }

        /// <inheritdoc />
        public ITimeline Reset()
        {
            _activeTimeline.Id = Master;

            _messageQueue.Alert(new Alerts.OnTimelineChange());
            _messageQueue.Alert(new Alerts.InvalidateProjections());

            return _activeTimeline;
        }
        
        // full clone of event store
        // can become really expensive
        // TODO: use links to event ids?
        private async Task Clone(string timeline, long time)
        {
            var cloneFlow = new CloneFlow(timeline, time, _eventStore);
            var streams = _eventStore.ListStreams()
                .Where(s => s.Key.StartsWith(_activeTimeline.Id))
                .Select(async s => { await cloneFlow.SendAsync(s); });

            streams.Finally(() => cloneFlow.Complete()).Subscribe(async t => await t);
            await cloneFlow.CompletionTask;
        }

        private long GetTime(string id)
        {
            if (id == Master)
                return DateTime.UtcNow.Ticks;
            if (_branches.TryGetValue(id, out var timeline))
                return timeline.Now;
            _log.Error($"Branch {id} does not exist!", this);
            throw new InvalidOperationException();
        }

        private class CloneFlow : Dataflow<IStream>
        {
            private readonly ActionBlock<IStream> _inputBlock;

            private readonly IEventStore<IAggregate> _eventStore;
            
            public CloneFlow(string timeline, long time, IEventStore<IAggregate> eventStore) 
                : base(DataflowOptions.Default)
            {
                _eventStore = eventStore;
                _inputBlock = new ActionBlock<IStream>(
                    async s =>
                {
                    var metadata = await _eventStore.ReadStream<IEventMetadata>(s, 0)
                        .LastOrDefaultAsync(e => e.Timestamp <= time);

                    if (metadata == null)
                        return;

                    var clone = s.Branch(timeline, metadata.Version);
                    await _eventStore.AppendToStream(clone);
                }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 8 });

                RegisterChild(_inputBlock);
            }

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }
    }
}