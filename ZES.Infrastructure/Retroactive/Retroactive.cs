using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
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
        private readonly ILog _log;
        private readonly ICommandRegistry _commandRegistry;

        private readonly IEsRepository<IAggregate> _repository;
        private readonly IEsRepository<ISaga> _sagaRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="Retroactive"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="sagaStore">Saga store</param>
        /// <param name="graph">Graph</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="streamLocator">Stream locator</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="repository">Aggregate repository</param>
        /// <param name="sagaRepository">Saga repository</param>
        /// <param name="log">Log service</param>
        /// <param name="commandRegistry">Command registry</param>
        public Retroactive(
            IEventStore<IAggregate> eventStore,
            IEventStore<ISaga> sagaStore,
            IGraph graph,
            IBranchManager manager,
            IStreamLocator streamLocator,
            IMessageQueue messageQueue,
            IEsRepository<IAggregate> repository,
            IEsRepository<ISaga> sagaRepository,
            ILog log, 
            ICommandRegistry commandRegistry)
        {
            _eventStore = eventStore;
            _graph = graph;
            _manager = manager;
            _streamLocator = streamLocator;
            _messageQueue = messageQueue;
            _sagaStore = sagaStore;
            _repository = repository;
            _sagaRepository = sagaRepository;
            _log = log;
            _commandRegistry = commandRegistry;
        }
        
        /// <inheritdoc />
        public async Task TrimStream(IStream stream, int version)
        {
            var store = GetStore(stream);
            await store.TrimStream(stream, version);
            await _graph.TrimStream(stream.Key, version);
            _messageQueue.Alert(new OnTimelineChange());
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IEvent>> ValidateDelete(IStream stream, int version) =>
            await Delete(stream, version, false);

        /// <inheritdoc />
        public async Task<bool> TryDelete(IStream stream, int version) =>
            !(await Delete(stream, version, true)).Any();

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
        public async Task<Dictionary<IStream, IEnumerable<IEvent>>> GetChanges(ICommand command, long time)
        {
            var activeBranch = _manager.ActiveBranch;
            var branch = $"{command.GetType().Name}-{time}";

            await _manager.Branch(branch, time);
            
            var handler = _commandRegistry.GetHandler(command);
            if (handler == null)
                throw new InvalidOperationException($"No handler found for command {command.GetType().Name}");
            await handler.Handle(command);
            
            await _manager.Branch(activeBranch);
            
            var dict = new Dictionary<IStream, IEnumerable<IEvent>>();
            
            var changes = await _manager.GetChanges(branch);

            foreach (var c in changes)
            {
                var stream = _streamLocator.FindBranched(c.Key, branch);
                var e = await _eventStore.ReadStream<IEvent>(stream, stream.Version - c.Value + 1, c.Value).ToList();

                dict[c.Key] = e;
            }

            await _manager.DeleteBranch(branch);
            return dict;
        }
        
        private async Task<bool> RollbackCommand(ICommand c)
        {
            var time = c.Timestamp - 1;
            var changes = await GetChanges(c, time);
            var canDelete = true;
            foreach (var change in changes)
            {
                foreach (var e in change.Value)
                    canDelete &= !(await ValidateDelete(change.Key, e.Version)).Any();
            }

            if (!canDelete)
                return false;
                
            foreach (var change in changes)
            {
                foreach (var e in change.Value)
                    await TryDelete(change.Key, e.Version);
            }

            return true;
        }

        private async Task<IEnumerable<IEvent>> Append(IStream stream, int version, IEnumerable<IEvent> events)
        {
            if (stream.Version != version - 1)
                throw new InvalidOperationException($"Version {version} is not consistent with stream version {stream.Version}");
            
            var enumerable = events.ToList();
            if (enumerable.Count == 0)
                return null;
            
            foreach (var e in enumerable)
            {
                e.Version = version;
                e.Stream = stream.Key;
                version++;
            }

            var store = GetStore(stream);
            await store.AppendToStream(stream, enumerable, false);
            
            // check if the resulting stream is valid
            return await GetInvalidEvents(stream);
        }

        private async Task<IEnumerable<IEvent>> Delete(IStream stream, int version, bool doDelete)
        {
            if (version == 0)
                return new List<IEvent> { new Event() };
            
            var store = GetStore(stream);
            var currentBranch = _manager.ActiveBranch;
            var liveStream = _streamLocator.FindBranched(stream, currentBranch);
 
            if (liveStream == null || liveStream.Version < version)
                return new List<IEvent> { new Event() };

            if (liveStream.Version == version)
            {
                if (doDelete)
                    await TrimStream(liveStream, version - 1);
                return new List<IEvent>();
            }
            
            var laterEvents = await store.ReadStream<IEvent>(liveStream, version + 1).ToList();
            var time = (await store.ReadStream<IEvent>(stream, version - 1, 1).SingleAsync()).Timestamp;

            var tempStreamId = $"{stream.Timeline}-{stream.Id}-{version}";
            var branch = await _manager.Branch(tempStreamId, time);

            var newStream = _streamLocator.FindBranched(stream, branch.Id);
            if (newStream == null)
                throw new InvalidOperationException($"Stream {tempStreamId}:{stream.Type}:{stream.Id} not found!");

            var invalidEvents = (await Append(newStream, version, laterEvents)).ToList();
            
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
            var liveStream = _streamLocator.FindBranched(stream, currentBranch);

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
                return null;
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