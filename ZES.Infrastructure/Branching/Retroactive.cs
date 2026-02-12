using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Branching
{
    /// <inheritdoc />
    public class Retroactive : IRetroactive
    {
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IEventStore<ISaga> _sagaStore;
        private readonly IBranchManager _manager;
        private readonly IGraph _graph;
        private readonly IStreamLocator _streamLocator;
        private readonly ILog _log;
        private readonly ICommandRegistry _commandRegistry;

        private readonly IEsRepository<IAggregate> _repository;
        private readonly IEsRepository<ISaga> _sagaRepository;
        private readonly IFlowCompletionService _flowCompletionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Retroactive"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="sagaStore">Saga store</param>
        /// <param name="graph">Graph</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="streamLocator">Stream locator</param>
        /// <param name="repository">Aggregate repository</param>
        /// <param name="sagaRepository">Saga repository</param>
        /// <param name="log">Log service</param>
        /// <param name="commandRegistry">Command registry</param>
        /// <param name="flowCompletionService">Flow completion service</param>
        public Retroactive(
            IEventStore<IAggregate> eventStore,
            IEventStore<ISaga> sagaStore,
            IGraph graph,
            IBranchManager manager,
            IStreamLocator streamLocator,
            IEsRepository<IAggregate> repository,
            IEsRepository<ISaga> sagaRepository,
            ILog log, 
            ICommandRegistry commandRegistry, 
            IFlowCompletionService flowCompletionService)
        {
            _eventStore = eventStore;
            _graph = graph;
            _manager = manager;
            _streamLocator = streamLocator;
            _sagaStore = sagaStore;
            _repository = repository;
            _sagaRepository = sagaRepository;
            _log = log;
            _commandRegistry = commandRegistry;
            _flowCompletionService = flowCompletionService;
        }
        
        /// <inheritdoc />
        public async Task TrimStream(IStream stream, int version)
        {
            if (stream == null)
                return;
            
            _log.StopWatch.Start("TrimStream");
            
            var store = GetStore(stream);

            if (version == ExpectedVersion.EmptyStream)
                await store.DeleteStream(stream);

            if (version > ExpectedVersion.EmptyStream && version < stream.Version)
            {
                await store.TrimStream(stream, version);
                await _graph.TrimStream(stream.Key, version);
            }
            
            _log.StopWatch.Stop("TrimStream");
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IEvent>> ValidateDelete(IStream stream, int version, int count = 1) =>
            await Delete(stream, version, count, false);

        /// <inheritdoc />
        public async Task<bool> TryDelete(IStream stream, int version, int count = 1) =>
            !(await Delete(stream, version, count, true)).Any();

        /// <inheritdoc />
        public async Task<IEnumerable<IEvent>> ValidateInsert(IStream stream, int version, IEnumerable<IEvent> events) =>
            await Insert(stream, version, events, false);

        /// <inheritdoc />
        public async Task<bool> TryInsertIntoStream(IStream stream, int version, IEnumerable<IEvent> events) =>
            !(await Insert(stream, version, events, true)).Any();

        /// <inheritdoc />
        public async Task<bool> RollbackCommands(IEnumerable<ICommand> commands)
        {
            var valid = true;
            var sortedCommands = commands.OrderBy(c => c.Timestamp);
            foreach (var c in sortedCommands.Reverse())
                valid &= await RollbackCommand(c);

            return valid;
        }

        /// <inheritdoc />
        public async Task<bool> ReplayCommand(ICommand c)
        {
            var type = typeof(RetroactiveCommand<>).MakeGenericType(c.GetType());
            var command = (ICommand)Activator.CreateInstance(type, c, c.Timestamp);
            var handler = _commandRegistry.GetHandler(command);
            await handler.Handle(command);
            return true;
        }

        /// <inheritdoc />
        public async Task<Dictionary<IStream, IEnumerable<IEvent>>> GetChanges(ICommand command)
        {
            var dict = new Dictionary<IStream, IEnumerable<IEvent>>();
            var streams = await _streamLocator.ListStreams(_manager.ActiveBranch);
            foreach (var s in streams)
            {
                IList<IEvent> events;
                if (!s.IsSaga)
                    events = await _eventStore.ReadStream<IEvent>(s, 0).Where(e =>
                        e.AncestorId == command.MessageId).ToList();
                else
                    events = await _sagaStore.ReadStream<IEvent>(s, 0).Where(e =>
                        e.AncestorId == command.MessageId).ToList();
                if(events.Count > 0)
                    dict[s] = events;
            }

            return dict;

        }
        
        /// <inheritdoc />
        public async Task<Dictionary<IStream, IEnumerable<IEvent>>> GetChanges(ICommand command, Time time)
        {
            var activeBranch = _manager.ActiveBranch;
            var branch = $"{command.GetType().Name}-{time.ToUnixTimeMilliseconds()}";

            _log.StopWatch.Start($"{nameof(GetChanges)}.Branch");
            await _manager.Branch(branch, time, deleteExisting: true);
            _log.StopWatch.Stop($"{nameof(GetChanges)}.Branch");

            _log.StopWatch.Start($"{nameof(GetChanges)}.HandleCommand");
            var copy = command.Copy();
            copy.RetroactiveId = default;
            copy.Timeline = branch;
            copy.StoreInLog = false;
            
            var handler = _commandRegistry.GetHandler(copy);
            if (handler == null)
                throw new InvalidOperationException($"No handler found for command {command.GetType().Name}");
            _flowCompletionService.TrackMessage(copy);
            await handler.Handle(copy);
            _flowCompletionService.MarkComplete(copy);
            await _flowCompletionService.NodeCompletionAsync(copy);
            _log.StopWatch.Stop($"{nameof(GetChanges)}.HandleCommand");
            
            _log.StopWatch.Start($"{nameof(GetChanges)}.Branch");
            await _manager.Branch(activeBranch);
            _log.StopWatch.Stop($"{nameof(GetChanges)}.Branch");
            
            var dict = new Dictionary<IStream, IEnumerable<IEvent>>();

            var changes = await _manager.GetChanges(branch);
            
            _log.StopWatch.Start("GetChanges.ReadStream");
            foreach (var c in changes)
            {
                var stream = await _streamLocator.FindBranched(c.Key, branch);
                var serializationType = Configuration.UseCompactDeserializationForRetroactiveOperations
                    ? SerializationType.Metadata
                    : SerializationType.PayloadAndMetadata;
                var eventStore = GetStore(stream);
                var e = await eventStore.ReadStream<IEvent>(stream, stream.Version - c.Value + 1, c.Value, serializationType).ToList();

                dict[c.Key] = e;
                // _log.Debug($"Recording change in stream {stream.Key}: events ({stream.Version - c.Value + 1}..{stream.Version})");
            }
            
            _log.StopWatch.Stop("GetChanges.ReadStream");

            _log.StopWatch.Start($"{nameof(GetChanges)}.{nameof(_manager.DeleteBranch)}");
            await _manager.DeleteBranch(branch);
            _log.StopWatch.Stop($"{nameof(GetChanges)}.{nameof(_manager.DeleteBranch)}");
            return dict;
        }
        
        /// <inheritdoc />
        public async Task<List<IEvent>> TryInsert(Dictionary<IStream, IEnumerable<IEvent>> changes, Time time)
        {
            var currentBranch = _manager.ActiveBranch;
            var tempStreamId = $"{currentBranch}-{time.ToUnixTimeMilliseconds()}";
            await _manager.Branch(tempStreamId, time, changes.Keys.Select(k => k.Key), true);

            var invalidEvents = new List<IEvent>();

            _log.StopWatch.Start("TryInsert.AppendChanges");
            var allEvents = new Dictionary<IStream, List<IEvent>>();
            foreach (var c in changes)
            {
                var stream = c.Key;
                var events = c.Value.ToList();

                var version = c.Key.Version + 1;
                
                var store = GetStore(stream);
                var liveStream = await _streamLocator.FindBranched(stream, currentBranch);

                IList<IEvent> laterEvents = new List<IEvent>();

                // remove the snapshot events from the future
                var serialisationType = Configuration.UseCompactDeserializationForRetroactiveOperations
                    ? SerializationType.Metadata
                    : SerializationType.PayloadAndMetadata;
                if (liveStream != null)
                    laterEvents = await store.ReadStream<IEvent>(liveStream, version, -1, serialisationType).Where(e => e is not ISnapshotEvent).ToList();

                var newStream = await _streamLocator.FindBranched(stream, tempStreamId) ?? stream.Branch(tempStreamId, ExpectedVersion.NoStream);
                // _log.Debug($"Inserting events ({version}..{events.Count + version}) into {newStream.Key}");
                var list = events.Concat(laterEvents).ToList();
                allEvents[stream] = list;
                var invalidStreamEvents = (await Append(newStream, version, list, laterEvents.Count > 0)).ToList();
                invalidEvents.AddRange(invalidStreamEvents);
            }
            
            _log.StopWatch.Stop("TryInsert.AppendChanges");
            await _manager.Branch(currentBranch);

            if (invalidEvents.Any())
            {
                await _manager.DeleteBranch(tempStreamId);
                return invalidEvents;
            }

            if (Configuration.DeleteStreamsInsteadOfTrimming)
            {
                _log.StopWatch.Start("TryInsert.Merge.DeleteStreams");
                foreach (var k in allEvents.Keys)
                {
                    var liveStream = await _streamLocator.FindBranched(k, currentBranch);
                    var store = GetStore(k);

                    var events = allEvents[k];
                    if (liveStream != null)
                    {
                        var serializationType = Configuration.UseCompactDeserializationForRetroactiveOperations
                            ? SerializationType.Metadata
                            : SerializationType.PayloadAndMetadata; 
                        var earlierEvents = await store.ReadStream<IEvent>(liveStream, 0, k.Version + 1, serializationType).ToList();

                        events = earlierEvents.Concat(events).ToList();
                        await store.DeleteStream(liveStream);
                    }

                    liveStream = k.Branch(currentBranch, ExpectedVersion.NoStream);

                    foreach (var e in events)
                    {
                        e.InTemporaryStream = true;
                        e.Stream = liveStream.Key;
                        e.Timeline = liveStream.Timeline;
                    }

                    _log.Debug($"Inserting events ({k.Version + 1}..{k.Version + 1 + changes[k].Count()}) into {liveStream.Key}");
                    await store.AppendToStream(liveStream, events, false);
                }    
                _log.StopWatch.Stop("TryInsert.Merge.DeleteStreams");
            }
            else
            {
                _log.StopWatch.Start("TryInsert.Merge.TrimStream");
                
                foreach (var k in allEvents.Keys)
                {
                    var liveStream = await _streamLocator.FindBranched(k, currentBranch);
                    await TrimStream(liveStream, k.Version);
                }
                
                _log.StopWatch.Stop("TryInsert.Merge.TrimStream");
                if (!Configuration.UseMergeForRetroactiveInsert)
                {
                    foreach (var l in allEvents)
                    {
                        var k = l.Key;
                        var events = l.Value;

                        var liveStream = await _streamLocator.FindBranched(k, currentBranch) ??
                                         k.Branch(currentBranch, ExpectedVersion.NoStream);

                        var store = GetStore(liveStream);
                        foreach (var e in events)
                        {
                            e.InTemporaryStream = false;
                            e.Stream = liveStream.Key;
                            e.Timeline = liveStream.Timeline;
                        }

                        _log.StopWatch.Start("TryInsert.Merge.AppendToStream");
                        await store.AppendToStream(liveStream, events, false);
                        _log.StopWatch.Stop("TryInsert.Merge.AppendToStream");
                    }
                }
                else
                {
                    await _manager.Merge(tempStreamId, true);
                }
                _log.StopWatch.Stop("TryInsert.Merge");
            }

            await _manager.DeleteBranch(tempStreamId);
            return invalidEvents;
        }
        
        private async Task<bool> RollbackCommand(ICommand c)
        {
            var time = c.Timestamp.JustBefore();
            var changes = await GetChanges(c, time);
            // var changes = await GetChanges(c);
            _log.Debug($"Rolling back command: {c}");
            // var changes = await GetChanges(c);
            var canDelete = true;
            foreach (var change in changes)
            {
                var rollbackMultipleEvents = Configuration.RollbackMultipleEventsAtOnce;
                var events = change.Value.OrderByDescending(x => x.Version).ToList();
                var version = events.Min(x => x.Version);
                var count = events.Count;
                rollbackMultipleEvents &= events.Max(x => x.Version) - version == count - 1; 
                if (rollbackMultipleEvents)
                {
                    _log.Debug($"Rolling back events ({version}..{version + count - 1}) from stream {change.Key.Key} for command {c.MessageId}");
                    var invalidEvents = (await ValidateDelete(change.Key, version, count)).ToList();
                    if (!invalidEvents.Any()) 
                        continue;
                    canDelete = false;
                    _log.Error($"Error rolling back events ({version}..{version + count - 1}) from stream {change.Key.Key} " +
                               $"with invalid events ({invalidEvents.Select(e => e.Version).Aggregate(string.Empty, (a,b) => $"{a},{b}")})");
                }
                else
                {
                    var eventsToDelete = new List<MessageId>();
                    foreach (var e in change.Value.OrderByDescending(x => x.Version))
                    {
                        _log.Debug($"Rolling back {change.Key}:{e.GetType().GetFriendlyName()} with version {e.Version} at {e.Timestamp}");
                        var invalidEvents = (await ValidateDelete(change.Key, e.Version)).ToList();
                        if (invalidEvents.Any(x => !eventsToDelete.Contains(x.MessageId)))
                        {
                            canDelete = false;
                            
                            _log.Error($"Error rolling back {change.Key}:{e.GetType().GetFriendlyName()} with version {e.Version}" +
                                       $" with invalid events ({invalidEvents.Select(e => e.Version).Aggregate(string.Empty, (a,b) => $"{a},{b}")})");
                            break;
                        }

                        var stream = change.Key;
                        var store = GetStore(stream);
                        var currentBranch = _manager.ActiveBranch;
                        var liveStream = await _streamLocator.FindBranched(stream, currentBranch);
                        eventsToDelete.Add((await store.ReadStream<IEvent>(liveStream, e.Version, 1)).MessageId);
                    }
                }
            }

            if (!canDelete)
            {
                _log.Error($"Cannot rollback command {c.GetType()}");
                return false;
            }

            foreach (var change in changes)
            {
                var rollbackMultipleEvents = Configuration.RollbackMultipleEventsAtOnce;
                var events = change.Value.OrderByDescending(x => x.Version).ToList();
                var version = events.Min(x => x.Version);
                var count = events.Count;
                rollbackMultipleEvents &= events.Max(x => x.Version) - version == count - 1;
                if (rollbackMultipleEvents)
                {
                    await TryDelete(change.Key, version, count);
                }
                else
                {
                    foreach (var e in change.Value.OrderByDescending(x => x.Version))
                        await TryDelete(change.Key, e.Version);
                }
            }

            return true;
        }

        private async Task<IEnumerable<IEvent>> Append(IStream stream, int version, IEnumerable<IEvent> events, bool checkInvalidEvents = true)
        {
            _log.StopWatch.Start(nameof(Append));
            var enumerable = events.ToList();
            if (enumerable.Count == 0)
                return enumerable;

            foreach (var e in enumerable)
            {
                e.InTemporaryStream = stream.IsTemporary;
                e.Version = version;
                e.Timeline = stream.Timeline;
                e.Stream = stream.Key;
                version++;
            }
            
            var store = GetStore(stream);
            _log.StopWatch.Start($"{nameof(Append)}.{nameof(store.AppendToStream)}");
            await store.AppendToStream(stream, enumerable, false).ConfigureAwait(false);
            _log.StopWatch.Stop($"{nameof(Append)}.{nameof(store.AppendToStream)}");

            // check if the resulting stream is valid
            IEnumerable<IEvent> invalidEvents = null;
            if (checkInvalidEvents)
                invalidEvents = await GetInvalidEvents(stream);
            
            _log.StopWatch.Stop(nameof(Append));
            return invalidEvents ?? new List<IEvent>();
        }

        private async Task<IEnumerable<IEvent>> Delete(IStream stream, int version, int count, bool doDelete)
        {
            var store = GetStore(stream);
            var currentBranch = _manager.ActiveBranch;
            var liveStream = await _streamLocator.FindBranched(stream, currentBranch);
            
            if (liveStream == null || liveStream.Version < version)
                return new List<IEvent>() { new Event() };

            var serialisationType = Configuration.UseCompactDeserializationForRetroactiveOperations
                ? SerializationType.Metadata
                : SerializationType.PayloadAndMetadata;
            
            if (liveStream.Version == version)
            {
                if (!doDelete)
                    return new List<IEvent>();

                if(!Configuration.DeleteStreamsInsteadOfTrimming)
                    await TrimStream(liveStream, version - count);
                else
                {
                    var events = await store.ReadStream<IEvent>(liveStream, 0, version - count + 1, serialisationType).ToList();
                    await store.DeleteStream(liveStream);
                    if (events.Count > 0)
                    {
                        liveStream = stream.Branch(currentBranch, ExpectedVersion.NoStream);
                        await store.AppendToStream(liveStream, events, false);
                    }
                }

                return new List<IEvent>();
            }
            
            var laterEvents = await store.ReadStream<IEvent>(liveStream, version + count).ToList();
            var time = await store.GetTimestamp(stream, version - 1);
            
            var tempStreamId = $"{stream.Timeline}-{stream.Id}-{version}";
            var branch = await _manager.Branch(tempStreamId, time);

            var newStream = await _streamLocator.FindBranched(stream, branch.Id);
            if (newStream == null)
                throw new InvalidOperationException($"Stream {tempStreamId}:{stream.Type}:{stream.Id} not found!");
             
            var invalidEvents = (await Append(newStream, version, laterEvents)).ToList();

            foreach (var e in invalidEvents)
                _log.Debug($"Invalid event {e.GetType().GetFriendlyName()} with version {e.Version} found in stream {newStream.Key}");
            
            await _manager.Branch(currentBranch);
            if (doDelete && !invalidEvents.Any())
            {
                if (!Configuration.DeleteStreamsInsteadOfTrimming)
                {
                    await TrimStream(liveStream, version - 1);
                    await _manager.Merge(tempStreamId);
                }
                else
                {
                    var events = await store.ReadStream<IEvent>(newStream, 0, -1, serialisationType).ToList();
                    await store.DeleteStream(liveStream);
                    liveStream = stream.Branch(currentBranch, ExpectedVersion.NoStream);
                    await store.AppendToStream(liveStream, events, false);
                }
            }

            await _manager.DeleteBranch(tempStreamId);

            return invalidEvents;
        }
        
        private async Task<IEnumerable<IEvent>> Delete(IStream stream, int version, bool doDelete)
        {
            if (version == 0)
                return new List<IEvent> { new Event() };
            
            var store = GetStore(stream);
            var currentBranch = _manager.ActiveBranch;
            var liveStream = await _streamLocator.FindBranched(stream, currentBranch);
 
            if (liveStream == null || liveStream.Version < version)
                return new List<IEvent> { new Event() };

            if (liveStream.Version == version)
            {
                if (doDelete)
                    await TrimStream(liveStream, version - 1);
                return new List<IEvent>();
            }
            
            var laterEvents = await store.ReadStream<IEvent>(liveStream, version + 1).ToList();
            var time = await store.GetTimestamp(stream, version - 1);
            // var time = (await store.ReadStream<IEvent>(stream, version - 1, 1).SingleAsync()).Timestamp;

            var tempStreamId = $"{stream.Timeline}-{stream.Id}-{version}";
            var branch = await _manager.Branch(tempStreamId, time);

            var newStream = await _streamLocator.FindBranched(stream, branch.Id);
            if (newStream == null)
                throw new InvalidOperationException($"Stream {tempStreamId}:{stream.Type}:{stream.Id} not found!");

            var invalidEvents = (await Append(newStream, version, laterEvents)).ToList();

            foreach (var e in invalidEvents)
                _log.Debug($"Invalid event {e.GetType().GetFriendlyName()} with version {e.Version} found in stream {newStream.Key}");    
            
            await _manager.Branch(currentBranch);
            if (doDelete && !invalidEvents.Any())
            {
                await TrimStream(liveStream, version - 1);
                await _manager.Merge(tempStreamId);
            }

            await _manager.DeleteBranch(tempStreamId);

            return invalidEvents;
        }
        
        private async Task<IEnumerable<IEvent>> Insert(IStream stream, int version, IEnumerable<IEvent> events, bool doInsert)
        {
            var store = GetStore(stream);
            var origVersion = version;
            var currentBranch = _manager.ActiveBranch;
            var liveStream = await _streamLocator.FindBranched(stream, currentBranch);

            IList<IEvent> laterEvents = new List<IEvent>();

            if (liveStream != null)
                laterEvents = await store.ReadStream<IEvent>(liveStream, version).ToList();
            else
                stream = stream.Branch(currentBranch, ExpectedVersion.EmptyStream);

            if (laterEvents.Count == 0)
            {
                if (doInsert)
                    return await Append(stream, version, events);

                if (stream.Version != version - 1)
                    throw new InvalidOperationException("Stream version is inconsistent");
                return new List<IEvent>();
            }

            var enumerable = events.ToList();
            Time time;
            if (version > 0)
                time = (await store.ReadStream<IEvent>(stream, version - 1, 1).SingleAsync()).Timestamp;
            else
                time = enumerable.First().Timestamp;

            var tempStreamId = $"{stream.Timeline}-{stream.Id}-{version}";
            var branch = await _manager.Branch(tempStreamId, time);

            var newStream = await _streamLocator.FindBranched(stream, branch.Id);
            if (newStream == null)
                throw new InvalidOperationException($"Stream {tempStreamId}:{stream.Type}:{stream.Id} not found!");

            var invalidEvents = (await Append(newStream, version, enumerable.Concat(laterEvents))).ToList();
            
            await _manager.Branch(currentBranch);
            if (doInsert && !invalidEvents.Any())
            {
                await TrimStream(liveStream, origVersion - 1);
                await _manager.Merge(tempStreamId);
            }
            
            await _manager.DeleteBranch(tempStreamId);
            return invalidEvents;
        }

        private async Task<IEnumerable<IEvent>> GetInvalidEvents(IStream stream)
        {
            var repository = GetRepository(stream);
            var invalidEvents = await repository.FindInvalidEvents(stream.Type, stream.Id);

            return invalidEvents;
        }
        
        private async Task<bool> IsValid(IStream stream)
        {
            var repository = GetRepository(stream);
            var lastValidVersion = await repository.LastValidVersion(stream.Type, stream.Id);
            
            if (lastValidVersion < stream.Version)
                _log.Warn($"Stream {stream.Key} will become invalid at {lastValidVersion + 1}", this);

            return lastValidVersion == stream.Version;
        }

        private IEventStore GetStore(IStream stream)
        {
            if (stream.IsSaga)
                return _sagaStore;
            return _eventStore;
        }

        private IEsRepository GetRepository(IStream stream)
        {
            if (stream.IsSaga)
                return _sagaRepository;
            return _repository;
        }
    }
}