using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Branching
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

            _branches.TryAdd(Master, Timeline.New(Master));
        }

        /// <inheritdoc />
        public async Task<ITimeline> Branch(string branchId, long? time = null)
        {
            if (_activeTimeline.Id == branchId)
                return _activeTimeline;

            var newBranch = !_branches.ContainsKey(branchId); // && branchId != Master;
            
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
            
            _log.Info($"Switched to {branchId} branch");
            
            // refresh the stream locator
            _messageQueue.Alert(new Alerts.OnTimelineChange());
            
            // rebuild all projections
            _messageQueue.Alert(new Alerts.InvalidateProjections());
                
            return _activeTimeline;
        }

        /// <inheritdoc />
        public async Task Merge(string branchId)
        {
            if (!_branches.TryGetValue(branchId, out var branch))
            {
                _log.Warn($"Branch {branchId} does not exist", this);
                return;
            }

            /*if (!branch.Live)
            {
                _log.Warn($"Trying to merge non-live branch {branchId}", this);
                return;
            }*/
            
            var mergeFlow = new MergeFlow(_activeTimeline, _eventStore, _streamLocator);

            _eventStore.ListStreams(branchId).Subscribe(mergeFlow.InputBlock.AsObserver());
            
            try
            {
                await mergeFlow.CompletionTask;
                mergeFlow.Complete();

                var mergeResult = mergeFlow.Result;
                _log.Info($"Merged {mergeResult.NumberOfStreams} streams, {mergeResult.NumberOfEvents} events from {branchId} into {_activeTimeline.Id}");
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
            Branch(Master).Wait();
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
            private int _numberOfEvents;
            private int _numberOfStreams;
            
            public MergeFlow(ITimeline currentBranch, IEventStore<IAggregate> eventStore, IStreamLocator<IAggregate> streamLocator) 
                : base(DataflowOptions.Default)
            {
                _inputBlock = new ActionBlock<IStream>(
                    async s =>
                    {
                        if (currentBranch != null && s.Parent != null && s.Parent.Timeline != currentBranch.Id && s.Parent.Version > ExpectedVersion.EmptyStream)
                            return;

                        var version = ExpectedVersion.EmptyStream;
                        var parentStream = s.Branch(currentBranch?.Id, ExpectedVersion.EmptyStream);
                        if (s.Parent != null && s.Parent.Version > ExpectedVersion.EmptyStream)
                        {
                            parentStream = streamLocator.Find(s.Parent);
                            version = parentStream.Version;

                            if (version > s.Parent.Version)
                            {
                                var theseEvents = await eventStore
                                    .ReadStream<IEvent>(parentStream, s.Parent.Version + 1, version - s.Parent.Version).Select(e => e.MessageId).ToList();
                                var thoseEvents = await eventStore
                                    .ReadStream<IEvent>(s, s.Parent.Version + 1, version - s.Parent.Version).Select(e => e.MessageId).ToList();
                                if (theseEvents.Zip(thoseEvents, (e1, e2) => (e1, e2)).Any(x => x.Item1 != x.Item2)) 
                                        throw new InvalidOperationException($"{currentBranch?.Id} timeline has moved on in the meantime, aborting...( {version} > {s.Parent.Version} )");
                                return;
                            }

                            if (s.Version == version)
                                return; 
                        }

                        Interlocked.Increment(ref _numberOfStreams);

                        var events = await eventStore.ReadStream<IEvent>(s, version + 1, s.Version - version).ToList();
                        foreach (var e in events.OfType<Event>())
                            e.Stream = parentStream.Key;

                        Interlocked.Add(ref _numberOfEvents, events.Count);
                        
                        await eventStore.AppendToStream(parentStream, events);
                }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Configuration.ThreadsPerInstance });

                RegisterChild(_inputBlock);
            }

            public MergeResult Result { get; } = new MergeResult(); 

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
            
            public override void Complete()
            {
                Result.NumberOfEvents = _numberOfEvents;
                Result.NumberOfStreams = _numberOfStreams;
                base.Complete();
            }
                
            public class MergeResult
            {
                public long NumberOfEvents { get; set; }
                public long NumberOfStreams { get; set; }
            }
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