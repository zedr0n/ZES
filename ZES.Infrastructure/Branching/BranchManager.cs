using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using NodaTime;
using NodaTime.Extensions;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
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
        private readonly IEventStore<ISaga> _sagaStore;
        private readonly ICommandLog _commandLog;
        private readonly IStreamLocator _streamLocator;
        private readonly IGraph _graph;

        /// <summary>
        /// Initializes a new instance of the <see cref="BranchManager"/> class.
        /// </summary>
        /// <param name="log">Application logger</param>
        /// <param name="activeTimeline">Root timeline</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="eventStore">Event store</param>
        /// <param name="sagaStore">Saga store</param>
        /// <param name="streamLocator">Stream locator</param>
        /// <param name="graph">Graph</param>
        /// <param name="commandLog">Command log</param>
        public BranchManager(
            ILog log, 
            ITimeline activeTimeline,
            IMessageQueue messageQueue, 
            IEventStore<IAggregate> eventStore,
            IEventStore<ISaga> sagaStore,
            IStreamLocator streamLocator,
            IGraph graph,
            ICommandLog commandLog)
        {
            _log = log;
            _activeTimeline = activeTimeline as Timeline;
            _messageQueue = messageQueue;
            _eventStore = eventStore;
            _streamLocator = streamLocator;
            _graph = graph;
            _commandLog = commandLog;
            _sagaStore = sagaStore;

            _branches.TryAdd(Master, Timeline.New(Master));
        }

        /// <inheritdoc />
        public IObservable<int> Ready =>
            _messageQueue.UncompletedMessages.Timeout(Configuration.Timeout).FirstAsync(s => s == 0);

        /// <inheritdoc />
        public string ActiveBranch => _activeTimeline.Id;

        /// <inheritdoc />
        public async Task DeleteBranch(string branchId)
        {
            _log.StopWatch.Start("DeleteBranch");
            await _messageQueue.UncompletedMessagesOnBranch(branchId).Timeout(Configuration.Timeout)
                .FirstAsync(s => s == 0);

            await DeleteBranch<IAggregate>(branchId);
            await DeleteBranch<ISaga>(branchId);
            await DeleteCommands(branchId);
            
            _branches.TryRemove(branchId, out var branch);
            _messageQueue.Alert(new BranchDeleted(branchId));
            
            _log.StopWatch.Stop("DeleteBranch");
        }

        /// <inheritdoc />
        public async Task<ITimeline> Branch(string branchId, Instant time = default, IEnumerable<string> keys = null, bool deleteExisting = false)
        {
            _log.StopWatch.Start("Branch");
            if (_activeTimeline.Id == branchId)
            {
                _log.Trace($"Already on branch {branchId}");
                return _activeTimeline;
            }

            _log.StopWatch.Start("Branch.Wait");
            await _messageQueue.UncompletedMessages.FirstAsync(s => s == 0).Timeout(Configuration.Timeout);
            _log.StopWatch.Stop("Branch.Wait");
            var newBranch = !_branches.ContainsKey(branchId); // && branchId != Master;
            if (!newBranch && deleteExisting)
            {
                await DeleteBranch(branchId);
                
                // branchId = branchId + "!";
                newBranch = true;
            }

            var timeline = _branches.GetOrAdd(branchId, b => Timeline.New(branchId, time));
            if (time != default && timeline.Now != time)
            {
                _log.Errors.Add(new InvalidOperationException($"Branch {branchId} already exists!"));
                return null;
            }
            
            _log.StopWatch.Start("Branch.Clone");
            
            // copy the events
            if (newBranch)
            {
                await Clone<IAggregate>(branchId, time != default ? time : SystemClock.Instance.GetCurrentInstant(), keys);
                await Clone<ISaga>(branchId, time != default ? time : SystemClock.Instance.GetCurrentInstant(), keys);
            }

            _log.StopWatch.Stop("Branch.Clone");

            // update current timeline
            _activeTimeline.Set(timeline);
            
            _log.Debug($"Switched to {branchId} branch");

            /* rebuild all projections
             _messageQueue.Alert(new Alerts.InvalidateProjections());*/
                
            _log.StopWatch.Stop("Branch");
            return _activeTimeline;
        }

        /// <inheritdoc />
        public async Task<Dictionary<IStream, int>> GetChanges(string branchId)
        {
            var changes = new ConcurrentDictionary<IStream, int>();
            if (!_branches.TryGetValue(branchId, out var branch))
            {
                _log.Warn($"Branch {branchId} does not exist", this);
                return new Dictionary<IStream, int>(changes);
            }

            if (_activeTimeline.Id == branchId)
            {
                _log.Warn($"Cannot merge branch into itself", this);
                return new Dictionary<IStream, int>(changes);
            }

            await StoreChanges(branchId, changes);
            /* await StoreChanges<IAggregate>(branchId, changes);
               await StoreChanges<ISaga>(branchId, changes); */

            return new Dictionary<IStream, int>(changes);
        }
        
        /// <inheritdoc />
        public async Task Merge(string branchId, bool includeNewStreams = true)
        {
            if (!_branches.TryGetValue(branchId, out var branch))
            {
                _log.Warn($"Branch {branchId} does not exist", this);
                return;
            }

            if (_activeTimeline.Id == branchId)
            {
                _log.Warn($"Cannot merge branch into itself", this);
                return;
            }

            /*if (!branch.Live)
            {
                _log.Warn($"Trying to merge non-live branch {branchId}", this);
                return;
            }*/

            var aggrResult = await MergeStore<IAggregate>(branchId, includeNewStreams);
            var sagaResult = await MergeStore<ISaga>(branchId, includeNewStreams);
            
            /*var mergeFlow = new MergeFlow(_activeTimeline, _eventStore, _streamLocator);

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
            }*/
            
            // rebuild all projections
            // _messageQueue.Alert(new Alerts.InvalidateProjections());
        }
        
        /// <inheritdoc />
        public ITimeline Reset()
        {
            Branch(Master).Wait();
            _messageQueue.Alert(new InvalidateProjections());
            return _activeTimeline;
        }

        private async Task DeleteCommands(string branchId)
        {
            _log.StopWatch.Start("DeleteCommands");
            await _commandLog.DeleteBranch(branchId);
            _log.StopWatch.Stop("DeleteCommands");
        }
        
        private async Task DeleteBranch<T>(string branchId)
            where T : IEventSourced
        {
            _log.StopWatch.Start("EventStore.DeleteBranch");
            var store = GetStore<T>();
            
            // var streams = await store.ListStreams(branchId).ToList();
            var streams = await _streamLocator.ListStreams<T>(branchId);
            foreach (var s in streams)
            {
                await store.DeleteStream(s);
                if (s.Version != s.Parent?.Version)
                    _log.Debug($"Deleted {typeof(T).Name} stream {s.Key}");
                await _graph.DeleteStream(s.Key);
            }
            
            _log.StopWatch.Stop("EventStore.DeleteBranch");
        }

        private async Task StoreChanges(string branchId, ConcurrentDictionary<IStream, int> changes)
        {
            var flow = new ChangesFlow(_activeTimeline, changes);
            var streams = await _streamLocator.ListStreams(branchId);
            streams.Subscribe(flow.InputBlock.AsObserver());

            try
            {
                await flow.CompletionTask;
            }
            catch (Exception e)
            {
                _log.Errors.Add(e);
            }
        }

        private async Task StoreChanges<T>(string branchId, ConcurrentDictionary<IStream, int> changes)
            where T : IEventSourced
        {
            var store = GetStore<T>();
            var flow = new ChangesFlow(_activeTimeline, changes);

            store.ListStreams(branchId).Subscribe(flow.InputBlock.AsObserver());

            try
            {
                await flow.CompletionTask;
            }
            catch (Exception e)
            {
                _log.Errors.Add(e);
            }
        }
        
        private async Task<MergeFlow<T>.MergeResult> MergeStore<T>(string branchId, bool includeNewStreams)
            where T : IEventSourced
        {
            var store = GetStore<T>();
            var mergeFlow = new MergeFlow<T>(_activeTimeline, store, _streamLocator);

            // store.ListStreams(branchId).Subscribe(mergeFlow.InputBlock.AsObserver());
            IEnumerable<IStream> streams; 
            var branchStreams = await _streamLocator.ListStreams<T>(branchId);
            if (includeNewStreams)
            {
                streams = branchStreams;
            }
            else
            {
                var currentStreams = await _streamLocator.ListStreams<T>(_activeTimeline.Id);
                streams = branchStreams.Intersect(currentStreams, new Stream.BranchComparer()).ToList();
            }
            
            streams.Subscribe(mergeFlow.InputBlock.AsObserver());
            
            try
            {
                await mergeFlow.CompletionTask;
                mergeFlow.Complete();

                var mergeResult = mergeFlow.Result;
                if (mergeResult.NumberOfStreams > 0)
                    _log.Debug($"Merged {mergeResult.NumberOfStreams} streams, {mergeResult.NumberOfEvents} events from {branchId} into {_activeTimeline.Id} [{typeof(T).Name}]");
                return mergeResult;
            }
            catch (Exception e)
            {
                _log.Errors.Add(e);
            }

            return null;
        }

        private IEventStore<T> GetStore<T>()
            where T : IEventSourced
        {
            if (_eventStore is IEventStore<T> store)
                return store;
            return _sagaStore as IEventStore<T>;
        }

        // full clone of event store
        // can become really expensive
        // TODO: use links to event ids?
        private async Task Clone<T>(string timeline, Instant time, IEnumerable<string> keys = null)
            where T : IEventSourced
        {
            var store = GetStore<T>();
            var cloneFlow = new CloneFlow<T>(timeline, time, store);
            
            // store.ListStreams(_activeTimeline.Id)
            var streams = await _streamLocator.ListStreams<T>(_activeTimeline.Id);
            streams.Where(s => keys == null || keys.Contains(s.Key))
                .Subscribe(cloneFlow.InputBlock.AsObserver());

            try
            {
                await cloneFlow.CompletionTask;
                if (cloneFlow.NumberOfStreams > 0)
                    _log.Debug($"{cloneFlow.NumberOfStreams} {typeof(T).Name} streams cloned to {timeline}");
            }
            catch (Exception e)
            {
                _log.Errors.Add(e);
            }
        }
        
        private class ChangesFlow : Dataflow<IStream>
        {
            private readonly ActionBlock<IStream> _inputBlock;
            
            public ChangesFlow(ITimeline currentBranch, ConcurrentDictionary<IStream, int> streams) 
                : base(Configuration.DataflowOptions)
            {
                _inputBlock = new ActionBlock<IStream>(
                    s =>
                    {
                        var version = ExpectedVersion.EmptyStream;
                        var parent = s.Parent;
                        while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
                        {
                            version = parent.Version;

                            if (s.Version == version)
                                return;

                            if (parent.Timeline == currentBranch?.Id)
                                break;

                            parent = parent.Parent;
                        }

                        var stream = parent ?? s.Branch(s.Timeline, version);
                        streams.TryAdd(stream, s.Version - version);
                    }, Configuration.DataflowOptions.ToExecutionBlockOption(true)); 

                RegisterChild(_inputBlock);
            }

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }
        
        private class MergeFlow<T> : Dataflow<IStream>
            where T : IEventSourced
        {
            private readonly ActionBlock<IStream> _inputBlock;
            private int _numberOfEvents;
            private int _numberOfStreams;
            
            public MergeFlow(ITimeline currentBranch, IEventStore<T> eventStore, IStreamLocator streamLocator) 
                : base(Configuration.DataflowOptions)
            {
                _inputBlock = new ActionBlock<IStream>(
                    async s =>
                    {
                        var version = ExpectedVersion.EmptyStream;
                        var parentStream = s.Branch(currentBranch?.Id, ExpectedVersion.EmptyStream);
                        var parent = s.Parent;
                        while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
                        {
                            parentStream = await streamLocator.Find(parent);
                            version = parentStream.Version;

                            if (currentBranch != null &&
                                (version > parent.Version && parent.Timeline == currentBranch.Id))
                            {
                                var theseEvents = await eventStore
                                    .ReadStream<IEvent>(parentStream, parent.Version + 1, version - parent.Version)
                                    .Select(e => e.MessageId).ToList();
                                var thoseEvents = await eventStore
                                    .ReadStream<IEvent>(s, parent.Version + 1, version - parent.Version)
                                    .Select(e => e.MessageId).ToList();
                                if (theseEvents.Zip(thoseEvents, (e1, e2) => (e1, e2)).Any(x => x.Item1 != x.Item2))
                                {
                                    throw new InvalidOperationException(
                                        $"{currentBranch?.Id} timeline has moved on in the meantime, aborting...( {parentStream.Key} : {version} > {parent.Version} )");
                                }

                                return;
                            }

                            if (s.Version == version)
                                return;

                            if (parentStream.Timeline == currentBranch?.Id)
                                break;

                            parent = parent.Parent;
                        }

                        Interlocked.Increment(ref _numberOfStreams);

                        var events = await eventStore.ReadStream<IEvent>(s, version + 1, s.Version - version).ToList();
                        foreach (var e in events.OfType<Event>())
                            e.Stream = parentStream.Key;

                        Interlocked.Add(ref _numberOfEvents, events.Count);

                        await eventStore.AppendToStream(parentStream, events, false);
                    }, Configuration.DataflowOptions.ToExecutionBlockOption(true)); 

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

        private class CloneFlow<T> : Dataflow<IStream>
            where T : IEventSourced
        {
            private readonly ActionBlock<IStream> _inputBlock;
            private int _numberOfStreams;
            
            public CloneFlow(string timeline, Instant time, IEventStore<T> eventStore) 
                : base(Configuration.DataflowOptions)
            {
                _inputBlock = new ActionBlock<IStream>(
                    async s =>
                    {
                        var version = await eventStore.GetVersion(s, time);
                        if (version == ExpectedVersion.NoStream)
                            return;

                        var clone = s.Branch(timeline, version);
                        await eventStore.AppendToStream(clone);
                        Interlocked.Increment(ref _numberOfStreams);
                    }, Configuration.DataflowOptions.ToExecutionBlockOption(true)); 

                RegisterChild(_inputBlock);
            }

            public int NumberOfStreams => _numberOfStreams;

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }
    }
}