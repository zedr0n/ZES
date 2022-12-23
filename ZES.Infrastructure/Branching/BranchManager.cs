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
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ExpectedVersion = SqlStreamStore.Streams.ExpectedVersion;
using IClock = ZES.Interfaces.Clocks.IClock;

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
        private readonly ConcurrentDictionary<string, ITimeline> _branches = new ConcurrentDictionary<string, ITimeline>();
        private readonly ITimeline _activeTimeline;
        private readonly IClock _clock;
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
        /// <param name="clock">Logical clock</param>
        public BranchManager(
            ILog log, 
            ITimeline activeTimeline,
            IMessageQueue messageQueue, 
            IEventStore<IAggregate> eventStore,
            IEventStore<ISaga> sagaStore,
            IStreamLocator streamLocator,
            IGraph graph,
            ICommandLog commandLog, 
            IClock clock)
        {
            _log = log;
            _activeTimeline = activeTimeline as Timeline;
            _messageQueue = messageQueue;
            _eventStore = eventStore;
            _streamLocator = streamLocator;
            _graph = graph;
            _commandLog = commandLog;
            _clock = clock;
            _sagaStore = sagaStore;

            _branches.TryAdd(Master, activeTimeline.New(Master));
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
        public async Task<ITimeline> Branch(string branchId, Time time = default, IEnumerable<string> keys = null, bool deleteExisting = false)
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

            var timeline = _branches.GetOrAdd(branchId, b => _activeTimeline.New(branchId, time));
            if (time != default && timeline.Now != time)
            {
                _log.Errors.Add(new InvalidOperationException($"Branch {branchId} already exists!"));
                return null;
            }
            
            _log.StopWatch.Start("Branch.Clone");
            
            // copy the events
            if (newBranch)
            {
                await Clone<IAggregate>(branchId, time ?? _clock.GetCurrentInstant()); 
                await Clone<ISaga>(branchId, time ?? _clock.GetCurrentInstant(), keys);
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

            return new Dictionary<IStream, int>(changes);
        }

        /// <inheritdoc />
        public Time GetTime(string branchId)
        {
            if (!_branches.TryGetValue(branchId, out var timeline))
                return Time.MinValue;

            return timeline.Now;
        }
        
        /// <inheritdoc />
        public async Task<MergeResult> Merge(string branchId, bool includeNewStreams = true)
        {
            if (!_branches.TryGetValue(branchId, out var branch))
            {
                _log.Warn($"Branch {branchId} does not exist", this);
                return new MergeResult(false, null, null);
            }

            if (_activeTimeline.Id == branchId)
            {
                _log.Warn($"Cannot merge branch into itself", this);
                return new MergeResult(false, null, null);
            }

            var aggrResult = await MergeStore<IAggregate>(branchId, includeNewStreams);
            var sagaResult = await MergeStore<ISaga>(branchId, includeNewStreams);
            
            if (aggrResult == null || sagaResult == null)
                return new MergeResult(false, null, null);

            var branchCommands = await _commandLog.GetCommands(branchId);
            var localCommands = await _commandLog.GetCommands(_activeTimeline.Id);
            var commandsToMerge = branchCommands.Except(localCommands).ToList();

            foreach (var c in commandsToMerge)
            {
                c.Timeline = _activeTimeline.Id;
                _commandLog.AppendCommand(c);
            }

            return new MergeResult(true, aggrResult.Concat(sagaResult).ToDictionary(x => x.Key, x => x.Value), commandsToMerge);
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
            await _commandLog.DeleteBranch(branchId);
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
        
        private async Task<Dictionary<IStream, int>> MergeStore<T>(string branchId, bool includeNewStreams)
            where T : IEventSourced
        {
            var store = GetStore<T>();

            // store.ListStreams(branchId).Subscribe(mergeFlow.InputBlock.AsObserver());
            IEnumerable<IStream> streams; 
            var branchStreams = await _streamLocator.ListStreams<T>(branchId);
            if (includeNewStreams)
            {
                streams = branchStreams.ToList();
            }
            else
            {
                var currentStreams = await _streamLocator.ListStreams<T>(_activeTimeline.Id);
                streams = branchStreams.Intersect(currentStreams, new Stream.BranchComparer()).ToList();
            }
            
            try
            {
                var mergeResult = new Dictionary<IStream, int>();
                if (!Configuration.UseLegacyMerge)
                {
                    // get merge changes
                    var mergeFlow = new MergeFlow<T>(_activeTimeline, store, _streamLocator);
                    streams.Subscribe(mergeFlow.InputBlock.AsObserver());
                    await mergeFlow.CompletionTask;
                    mergeFlow.Complete();
                    var changes = mergeFlow.Changes;
                    
                    // apply changes
                    var appendFlow = new AppendFlow<T>(store);
                    await appendFlow.ProcessAsync(changes.Select(x => (x.Key, x.Value)));
                    
                    // record results
                    foreach (var c in changes)
                        mergeResult.Add(c.Key, c.Value?.Count ?? 0);        
                }
                else
                {
                    // check any errors on the merge
                    var mergeFlow = new LegacyMergeFlow<T>(_activeTimeline, store, _streamLocator, false);
                    streams.Subscribe(mergeFlow.InputBlock.AsObserver());
                    await mergeFlow.CompletionTask;
                    mergeFlow.Complete();

                    // actually do the merge
                    mergeFlow = new LegacyMergeFlow<T>(_activeTimeline, store, _streamLocator, true);
                    streams.Subscribe(mergeFlow.InputBlock.AsObserver());
                    await mergeFlow.CompletionTask;
                    mergeFlow.Complete();

                    mergeResult = new Dictionary<IStream, int>(mergeFlow.Streams);
                }

                if (mergeResult.Count > 0)
                {
                    _log.Info(
                        $"Merged {mergeResult.Count} streams, {mergeResult.Values.Sum()} events from {branchId} into {_activeTimeline.Id} [{typeof(T).Name}]");
                }

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
        private async Task Clone<T>(string timeline, Time time, IEnumerable<string> keys = null)
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
                    }, Configuration.DataflowOptions.ToDataflowBlockOptions(true)); // ToExecutionBlockOption(true)); 

                RegisterChild(_inputBlock);
            }

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }
        
        private class MergeFlow<T> : Dataflow<IStream>
            where T : IEventSourced
        {
            private readonly ActionBlock<IStream> _inputBlock;

            public MergeFlow(ITimeline currentBranch, IEventStore<T> eventStore, IStreamLocator streamLocator) 
                : base(Configuration.DataflowOptions)
            {
                _inputBlock = new ActionBlock<IStream>(
                    async s =>
                    {
                        // find the common base
                        var baseStream = await streamLocator.FindBranched(s, currentBranch?.Id);
                        var streamNotFound = baseStream == null;
                        if (streamNotFound)
                             baseStream = s.Branch(currentBranch?.Id, s.Parent?.Version ?? ExpectedVersion.NoStream);

                        // find common version
                        /*var baseAncestors = baseStream.Ancestors.ToList();
                        var branchAncestors = s.Ancestors.ToList();
                        var commonAncestors = baseAncestors.Intersect(branchAncestors, new Stream.StreamComparer()).ToList();
                        if (commonAncestors.Count > 1)
                        {
                            throw new InvalidOperationException(
                                $"{s} and {baseStream} have multiple common ancestors!");
                        }

                        var commonAncestor = commonAncestors.SingleOrDefault();
                        if (commonAncestor == null)
                        {
                            throw new InvalidOperationException(
                                $"Couldn't find common ancestor for {s} and {baseStream}");
                        }*/
                        
                        var minVersion = Math.Min(s.Version, baseStream.Version);
                        
                        // verify fast-forward
                        var baseHash = await eventStore.GetHash(baseStream, minVersion);
                        var branchHash = await eventStore.GetHash(s, minVersion);
                        if (baseHash != branchHash)
                        {
                            throw new InvalidOperationException(
                                $"{s.Key} and {baseStream.Key} do not agree at latest version {minVersion}");
                        }

                        if (s.Version > minVersion)
                        {
                            var events = await eventStore.ReadStream<IEvent>(s, minVersion + 1, s.Version - minVersion).ToList();
                            foreach (var e in events.OfType<Event>())
                            {
                                e.Stream = baseStream.Key;
                                e.Timeline = currentBranch?.Id;
                            }

                            Changes.TryAdd(baseStream, events.ToList());
                        }
                        else if (streamNotFound)
                        {
                            Changes.TryAdd(baseStream, null);
                        }
                    }, Configuration.DataflowOptions.ToDataflowBlockOptions(true)); // .ToExecutionBlockOption(true)); 

                RegisterChild(_inputBlock);
            }

            public ConcurrentDictionary<IStream, List<IEvent>> Changes { get; } = new ();
            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }

        private class LegacyMergeFlow<T> : Dataflow<IStream>
            where T : IEventSourced
        {
            private readonly ActionBlock<IStream> _inputBlock;

            public LegacyMergeFlow(ITimeline currentBranch, IEventStore<T> eventStore, IStreamLocator streamLocator, bool doMerge) 
                : base(Configuration.DataflowOptions)
            {
                _inputBlock = new ActionBlock<IStream>(
                    async s =>
                    {
                        var version = ExpectedVersion.EmptyStream;
                        var parentStream = s.Branch(currentBranch?.Id, ExpectedVersion.EmptyStream);
                        var parent = s.Parent;

                        // get base
                        var branchStream = await streamLocator.FindBranched(s, currentBranch?.Id);
                        if (branchStream != null && parent == null)
                        {
                            version = branchStream.Version;
                            parentStream = branchStream;
                            parent = branchStream;
                        }
                        
                        while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
                        {
                            parentStream = await streamLocator.Find(parent);
                            version = parentStream.Version;

                            if (currentBranch != null &&
                                (version > parent.Version && parent.Timeline == currentBranch.Id))
                            {
                                var parentStreamHash = await eventStore.GetHash(parentStream, version);
                                var streamHash = await eventStore.GetHash(s, version);
                                if (parentStreamHash != streamHash)
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

                        if (!doMerge)
                            return;

                        var events = await eventStore.ReadStream<IEvent>(s, version + 1, s.Version - version).ToList();
                        foreach (var e in events.OfType<Event>())
                            e.Stream = parentStream.Key;

                        await eventStore.AppendToStream(parentStream, events, false);
                        Streams.TryAdd(parentStream, events.Count());
                    }, Configuration.DataflowOptions.ToDataflowBlockOptions(true)); // .ToExecutionBlockOption(true)); 

                RegisterChild(_inputBlock);
            }

            public ConcurrentDictionary<IStream, int> Streams { get; } = new ();

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }

        private class CloneFlow<T> : Dataflow<IStream>
            where T : IEventSourced
        {
            private readonly ActionBlock<IStream> _inputBlock;
            private int _numberOfStreams;
            
            public CloneFlow(string timeline, Time time, IEventStore<T> eventStore) 
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
                    }, Configuration.DataflowOptions.ToDataflowBlockOptions(true)); // .ToExecutionBlockOption(true)); 

                RegisterChild(_inputBlock);
            }

            public int NumberOfStreams => _numberOfStreams;

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }
    }
}