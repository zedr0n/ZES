using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Infrastructure;
using ExpectedVersion = ZES.Infrastructure.EventStore.ExpectedVersion;
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
        private readonly ConcurrentDictionary<string, ITimeline> _branches = new();
        private readonly IActiveTimeline _activeTimeline;
        private readonly IClock _clock;
        private readonly IBus _bus;
        private readonly IMessageQueue _messageQueue;
        private readonly IFlowCompletionService _flowCompletionService;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IEventStore<ISaga> _sagaStore;
        private readonly ICommandLog _commandLog;
        private readonly IStreamLocator _streamLocator;
        private readonly IGraph _graph;

        private readonly ConcurrentDictionary<string, WakeState> _wakeStates = new();
        private readonly Lock _wakeLock = new();

        private class WakeState
        {
            public CancellationTokenSource Source { get; set; }
            public SemaphoreSlim AdvanceLock { get; } = new(1, 1);
            public IDisposable Subscription { get; set; }
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BranchManager"/> class.
        /// </summary>
        /// <param name="log">Application logger</param>
        /// <param name="activeTimeline">Root timeline</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="flowCompletionService"></param>
        /// <param name="eventStore">Event store</param>
        /// <param name="sagaStore">Saga store</param>
        /// <param name="streamLocator">Stream locator</param>
        /// <param name="graph">Graph</param>
        /// <param name="commandLog">Command log</param>
        /// <param name="clock">Logical clock</param>
        /// <param name="bus">Command bus</param>       
        public BranchManager(
            ILog log, 
            IActiveTimeline activeTimeline,
            IMessageQueue messageQueue,
            IFlowCompletionService flowCompletionService,
            IEventStore<IAggregate> eventStore,
            IEventStore<ISaga> sagaStore,
            IStreamLocator streamLocator,
            IGraph graph,
            ICommandLog commandLog, 
            IClock clock,
            IBus bus)
        {
            _log = log;
            _activeTimeline = activeTimeline;
            _messageQueue = messageQueue;
            _flowCompletionService = flowCompletionService;
            _eventStore = eventStore;
            _streamLocator = streamLocator;
            _graph = graph;
            _commandLog = commandLog;
            _clock = clock;
            _bus = bus;
            _sagaStore = sagaStore;

            var master = activeTimeline.Timeline;
            _branches.TryAdd(Master, master);
            master.PendingCommandsChanged.Subscribe(_ => ScheduleWake(Master));
        }

        /// <inheritdoc />
        public Task Ready =>
            _flowCompletionService.CompletionAsync(includeRetroactive: true).Timeout(Configuration.Timeout);
            //_messageQueue.UncompletedMessages.Timeout(Configuration.Timeout).FirstAsync(s => s == 0);

        /// <inheritdoc />
        public string ActiveBranch => _activeTimeline.Id;

        /// <inheritdoc />
        public async Task DeleteBranch(string branchId)
        {
            _log.StopWatch.Start("DeleteBranch");
            
            _log.StopWatch.Start($"{nameof(DeleteBranch)}.Wait");
            //await _messageQueue.UncompletedMessagesOnBranch(branchId).Timeout(Configuration.Timeout)
            //    .FirstAsync(s => s == 0);            
            await _flowCompletionService.CompletionAsync(branchId).Timeout(Configuration.Timeout);
            _log.StopWatch.Stop($"{nameof(DeleteBranch)}.Wait");

            
            var deleteAggregate = DeleteBranch<IAggregate>(branchId);
            var deleteSaga = DeleteBranch<ISaga>(branchId);
            var deleteCommands = DeleteCommands(branchId);
            await Task.WhenAll(deleteAggregate, deleteSaga, deleteCommands);
            
            _branches.TryRemove(branchId, out var branch);
            if (branch is { Live: true })
            {
                if (_wakeStates.TryRemove(branchId, out var wakeState))
                {
                    wakeState.Source?.Cancel();
                    wakeState.Subscription?.Dispose();
                }
            }
                
            _messageQueue.Alert(new BranchDeleted(branchId));
            
            _log.StopWatch.Stop("DeleteBranch");
        }

        /// <summary>
        /// Creates or switches to an existing branch with the specified parameters.
        /// </summary>
        /// <param name="branchId">The unique identifier of the branch to create or switch to.</param>
        /// <param name="time">The point in time at which the branch begins, or null for a live timeline.</param>
        /// <param name="keys">An optional collection of keys for filtering the events to clone into the branch.</param>
        /// <param name="deleteExisting">Indicates whether to delete the existing branch with the same identifier, if it exists.</param>
        /// <param name="useLazy">Specifies whether lazy branching should be used; if null, the default configuration is applied.</param>
        /// <returns>The timeline associated with the specified branch, or null if the branch could not be created or switched to.</returns>
        public async Task<ITimeline> Branch(string branchId, Time time = null, IEnumerable<string> keys = null, bool deleteExisting = false, bool? useLazy = null)
        {
            var lazy = Configuration.UseLazyBranching;
            if (useLazy.HasValue)
                lazy = useLazy.Value;
            
            _log.StopWatch.Start("Branch");
            if (_activeTimeline.Id == branchId)
            {
                _log.Debug($"Already on branch {branchId}");
                return _activeTimeline;
            }

            _log.StopWatch.Start("Branch.Wait");
            await _flowCompletionService.CompletionAsync().Timeout(Configuration.Timeout);
            _log.StopWatch.Stop("Branch.Wait");
            var newBranch = !_branches.ContainsKey(branchId);
            if (!newBranch && deleteExisting)
            {
                await DeleteBranch(branchId);
                newBranch = true;
            }
            
            var timeline = _branches.GetOrAdd(branchId, b => _activeTimeline.New(branchId, time));
            if (time != null && timeline.Now != time)
            {
                _log.Errors.Add(new InvalidOperationException($"Branch {branchId} already exists!"));
                return null;
            }
            
            _log.StopWatch.Start("Branch.Clone");
            
            // copy the events
            if (newBranch)
            {
                var t = time ?? _clock.GetCurrentInstant();
                await Task.WhenAll(
                    Clone<IAggregate>(branchId, t, null, lazy),
                    Clone<ISaga>(branchId, t, keys, lazy));
            }

            _log.StopWatch.Stop("Branch.Clone");
           
            if (time == null)
                CancelWake(_activeTimeline.Timeline.Id);
            
            // update current timeline
            _activeTimeline.Timeline = timeline;
            _log.Debug($"Switched to {branchId} branch");
            
            // set the scheduler for live timelines
            if (time == null)
            {
                ScheduleWake(branchId);

                if (newBranch)
                {
                    lock (_wakeStates)
                    {
                        var wakeState = _wakeStates.GetOrAdd(branchId, _ => new WakeState());
                        wakeState.Subscription = timeline.PendingCommandsChanged.Subscribe(_ => ScheduleWake(branchId));
                    }
                }
            }

            _log.StopWatch.Stop("Branch");
            return timeline;
        }

        /// <inheritdoc />
        public async Task<ITimeline> Advance(string branchId, Time time)
        {
            if (!_branches.TryGetValue(branchId, out var timeline))
                return null;

            var effectiveUntil = timeline.Live && time > timeline.Now ? timeline.Now : time; 
            while ( (timeline.PeekCommand()?.Timestamp ?? Time.MaxValue) <= effectiveUntil) 
            {
                var command = timeline.DequeCommand();
                timeline.Advance(command.Timestamp);
                await _bus.Command(command.ToRetroactiveCommand(command.Timestamp));
            }
            
            timeline.Advance(time);
            return timeline;
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

            _messageQueue.Alert(new ImmediateInvalidateProjections());
            return new MergeResult(true, aggrResult.Concat(sagaResult).ToDictionary(x => x.Key, x => x.Value), commandsToMerge);
        }
        
        /// <inheritdoc />
        public ITimeline Reset()
        {
            Branch(Master).Wait();
            _messageQueue.Alert(new ImmediateInvalidateProjections());
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
            var streams = await _streamLocator.ListStreams(branchId);
            foreach (var s in streams)
                ProcessChange(s, _activeTimeline, changes);
        }

        private async Task StoreChanges<T>(string branchId, ConcurrentDictionary<IStream, int> changes)
            where T : IEventSourced
        {
            var store = GetStore<T>();
            var streams = await store.ListStreams(branchId).ToList();
            foreach (var s in streams)
                ProcessChange(s, _activeTimeline, changes);
        }

        private static void ProcessChange(IStream s, ITimeline currentBranch, ConcurrentDictionary<IStream, int> changes)
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
            changes.TryAdd(stream, s.Version - version);
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
        private async Task Clone<T>(string timeline, Time time, IEnumerable<string> keys = null, bool lazy = false)
            where T : IEventSourced
        {
            var store = GetStore<T>();
            var streams = (await _streamLocator.ListStreams<T>(_activeTimeline.Id))
                .Where(s => keys == null || keys.Contains(s.Key)).ToList();
            
            if (streams.Count == 0)
                return;
            
            var count = 0;
            var tasks = streams.Select(async s =>
            {
                var version = await store.GetVersion(s, time);
                if (version == ExpectedVersion.NoStream)
                    return;
                var clone = s.Branch(timeline, version);
                if (lazy)
                    _streamLocator.Register(clone);
                else
                    await store.AppendToStream(clone);
                Interlocked.Increment(ref count);
            });
            await Task.WhenAll(tasks);            

            if (count > 0)
                _log.Debug($"{count} {typeof(T).Name} streams cloned to {timeline}");            
            
        }

        private void CancelWake(string branchId)
        {
            lock (_wakeLock)
            {
                if (branchId != _activeTimeline.Id)
                    return;
                
                if (!_wakeStates.TryGetValue(branchId, out var state))
                    return;
                
                state.Source?.Cancel();
                state.Subscription?.Dispose();
            }
        }
        
        private void ScheduleWake(string branchId)
        {
            lock (_wakeLock)
            {
                if (branchId != _activeTimeline.Id)
                    return;
                
                if (!_branches.TryGetValue(branchId, out var timeline))
                    return;
                
                var wakeState = _wakeStates.GetOrAdd(branchId, _ => new WakeState());
                wakeState.Source?.Cancel();
                
                var next = timeline.PeekCommand()?.Timestamp;
                if (next == null)
                    return;
               
                var wakeCts = new CancellationTokenSource();
                wakeState.Source = wakeCts;

                _ = WakeTimelineAt(branchId, next, wakeState.Source, wakeState.AdvanceLock);
            }
        }    
        
        private async Task WakeTimelineAt(string timeline, Time dueAt, CancellationTokenSource source, SemaphoreSlim advanceLock)
        {
            var token = source.Token;
            try
            {
                var now = _clock.GetCurrentInstant();
                var delay = dueAt > now
                    ? (dueAt - now).ToTimeSpan()
                    : TimeSpan.Zero;

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, token);

                if (token.IsCancellationRequested)
                    return;

                await advanceLock.WaitAsync(token);
                try
                {
                    await Advance(timeline, _clock.GetCurrentInstant());
                }
                finally
                {
                    advanceLock.Release();
                    ScheduleWake(timeline);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                if (!_wakeStates.TryGetValue(timeline, out var state) || !ReferenceEquals(state.Source, source))
                    source.Dispose();            }
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
                        {
                            e.Stream = parentStream.Key;
                        }

                        await eventStore.AppendToStream(parentStream, events, false);
                        Streams.TryAdd(parentStream, events.Count());
                    }, Configuration.DataflowOptions.ToDataflowBlockOptions(true)); // .ToExecutionBlockOption(true)); 

                RegisterChild(_inputBlock);
            }

            public ConcurrentDictionary<IStream, int> Streams { get; } = new ();

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }
    }
}