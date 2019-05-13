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
        /// <summary>
        /// Gets root timeline id
        /// </summary>
        /// <value>
        /// Root timeline id
        /// </value>
        public const string Master = "master";
        
        private readonly ILog _log;
        private readonly ConcurrentDictionary<string, Timeline> _branches = new ConcurrentDictionary<string, Timeline>();
        private readonly Timeline _activeTimeline;
        private readonly IMessageQueue _messageQueue;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IStreamLocator<IAggregate> _streamLocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="BranchManager"/> class.
        /// </summary>
        /// <param name="log">Application logger</param>
        /// <param name="activeTimeline">Root timeline</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="eventStore">Event store</param>
        /// <param name="streamLocator">Stream locator</param>
        public BranchManager(
            ILog log, 
            ITimeline activeTimeline,
            IMessageQueue messageQueue, 
            IEventStore<IAggregate> eventStore,
            IStreamLocator<IAggregate> streamLocator)
        {
            _log = log;
            _activeTimeline = activeTimeline as Timeline;
            _messageQueue = messageQueue;
            _eventStore = eventStore;
            _streamLocator = streamLocator;
        }

        /// <inheritdoc />
        public async Task<ITimeline> Branch(string branchId, long? time = null)
        {
            if (_activeTimeline.Id == branchId)
                return _activeTimeline;
            
            var newBranch = !_branches.ContainsKey(branchId);
            
            var timeline = _branches.GetOrAdd(branchId, b => Timeline.New(branchId, time));
            if (time != null && timeline.Now != time.Value)
            {
                _log.Errors.Add(new InvalidOperationException($"Branch ${branchId} already exists!"));
                return null;
            }
            
            // copy the events
            if (newBranch)
                await Clone(branchId, time ?? DateTime.UtcNow.Ticks);

            // update current timeline
            _activeTimeline.Set(timeline);
            
            _log.Info($"Switched to {branchId}", this);
            
            // refresh the stream locator
            _messageQueue.Alert(new Alerts.OnTimelineChange());
            
            // rebuild all projections
            _messageQueue.Alert(new Alerts.InvalidateProjections());
                
            return _activeTimeline;
        }

        /// <inheritdoc />
        public async Task Merge(string branchId)
        {
            if (_activeTimeline.Id != Master)
                return;

            if (!_branches.TryGetValue(branchId, out var branch))
            {
                _log.Warn($"Branch {branchId} does not exist", this);
                return;
            }

            if (!branch.Live)
            {
                _log.Warn($"Trying to merge non-live branch {branchId}", this);
                return;
            }

            var mergeFlow = new MergeFlow(_eventStore, _streamLocator);

            _eventStore.ListStreams(branchId).Subscribe(mergeFlow.InputBlock.AsObserver());
            
            try
            {
                await mergeFlow.CompletionTask;
            }
            catch (Exception e)
            {
                _log.Errors.Add(e);
            }
            
            // rebuild all projections
            _messageQueue.Alert(new Alerts.InvalidateProjections());
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
            _eventStore.ListStreams(_activeTimeline.Id)
                .Subscribe(cloneFlow.InputBlock.AsObserver());

            try
            {
                await cloneFlow.CompletionTask;
            }
            catch (Exception e)
            {
                _log.Errors.Add(e);
            }
        }

        private class MergeFlow : Dataflow<IStream>
        {
            private readonly ActionBlock<IStream> _inputBlock;

            public MergeFlow(IEventStore<IAggregate> eventStore, IStreamLocator<IAggregate> streamLocator) 
                : base(DataflowOptions.Default)
            {
                _inputBlock = new ActionBlock<IStream>(
                    async s =>
                {
                    if (s.Parent?.Timeline != Master)
                        throw new NotImplementedException("Can only merge direct children for now");

                    var masterStream = streamLocator.Find(s.Parent);
                    var version = masterStream.Version;
                    
                    if ( version > s.Parent.Version )
                        throw new InvalidOperationException($"Master timeline has moved on in the meantime, aborting...( {version} > {s.Parent.Version} )");

                    if (s.Version == version)
                        return;

                    var events = await eventStore.ReadStream<IEvent>(s, version + 1, s.Version - version).ToList();
                    foreach (var e in events.OfType<Event>())
                        e.Stream = masterStream.Key;
                    
                    await eventStore.AppendToStream(masterStream, events);
                }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Configuration.ThreadsPerInstance });

                RegisterChild(_inputBlock);
            }

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }

        private class CloneFlow : Dataflow<IStream>
        {
            private readonly ActionBlock<IStream> _inputBlock;

            public CloneFlow(string timeline, long time, IEventStore<IAggregate> eventStore) 
                : base(DataflowOptions.Default)
            {
                _inputBlock = new ActionBlock<IStream>(
                    async s =>
                {
                    var metadata = await eventStore.ReadStream<IEventMetadata>(s, 0)
                        .LastOrDefaultAsync(e => e.Timestamp <= time);

                    if (metadata == null)
                        return;

                    var clone = s.Branch(timeline, metadata.Version);
                    await eventStore.AppendToStream(clone);
                }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Configuration.ThreadsPerInstance });

                RegisterChild(_inputBlock);
            }

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }
    }
}