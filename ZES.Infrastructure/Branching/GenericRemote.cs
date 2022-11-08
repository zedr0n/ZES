﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Branching
{
    /// <inheritdoc />
    public class GenericRemote : IRemote
    {
        private readonly IEventStore<IAggregate> _aggregateStore;
        private readonly IEventStore<ISaga> _sagaStore;
        private readonly IEventStore<IAggregate> _remoteAggregateStore;
        private readonly IEventStore<ISaga> _remoteSagaStore;
        private readonly ICommandLog _commandLog;
        private readonly ICommandLog _remoteCommandLog;
        private readonly ILog _log;
        private readonly IBranchManager _branchManager;
        private readonly IStreamLocator _streamLocator;

        private readonly ConcurrentDictionary<string, EventId> _replicaLatestSynced = new ConcurrentDictionary<string, EventId>();

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericRemote"/> class.
        /// </summary>
        /// <param name="aggregateStore">Local aggregate store</param>
        /// <param name="sagaStore">Local saga store </param>
        /// <param name="remoteAggregateStore">Remote aggregate store</param>
        /// <param name="remoteSagaStore">Remote saga store</param>
        /// <param name="commandLog">Command log</param>
        /// <param name="remoteCommandLog">Remote command log</param>
        /// <param name="log">Logging service</param>
        /// <param name="branchManager">Branch manager service</param>
        /// <param name="streamLocator">Stream locator service</param>
        public GenericRemote(
            IEventStore<IAggregate> aggregateStore,
            IEventStore<ISaga> sagaStore,
            [Remote] IEventStore<IAggregate> remoteAggregateStore,
            [Remote] IEventStore<ISaga> remoteSagaStore,
            ICommandLog commandLog, 
            [Remote] ICommandLog remoteCommandLog,
            ILog log,
            IBranchManager branchManager,
            IStreamLocator streamLocator)
            {
            _aggregateStore = aggregateStore;
            _sagaStore = sagaStore;
            _remoteAggregateStore = remoteAggregateStore;
            _remoteSagaStore = remoteSagaStore;
            _log = log;
            _branchManager = branchManager;
            _streamLocator = streamLocator;
            _commandLog = commandLog;
            _remoteCommandLog = remoteCommandLog;
        }

        /// <summary>
        /// Gets or sets the replica name
        /// </summary>
        public string ReplicaName { get; set; } = "Server";

        /// <inheritdoc />
        public async Task<FastForwardResult> Push(string branchId)
        {
            var pushResult = new FastForwardResult() { ResultStatus = FastForwardResult.Status.Failed };

            var activeTimeline = _branchManager.ActiveBranch;
            var syncTime = _branchManager.GetTime(_branchManager.ActiveBranch);
            var syncRemoteTimeline = $"{branchId}-{ReplicaName}-{syncTime.ToUnixTimeMilliseconds()}";

            await CopyRemoteToTimeline<IAggregate>(branchId, syncRemoteTimeline);
            await CopyRemoteToTimeline<ISaga>(branchId, syncRemoteTimeline);
            await _branchManager.Branch(syncRemoteTimeline);
            var mergeResult = await _branchManager.Merge(branchId);
            await _branchManager.Branch(activeTimeline);

            if (mergeResult.success)
            {
                pushResult.ResultStatus = FastForwardResult.Status.Success;
                var changes = mergeResult.changes;
                var commandChanges = mergeResult.commandChanges;
                    
                foreach (var v in changes.Where(v => !v.Key.IsSaga))
                {
                    var localStore = GetEventStore(v.Key, false);
                    var remoteStore = GetEventStore(v.Key, true);
                    var remoteStreams = await remoteStore.ListStreams(branchId).ToList();
                    var s = await _streamLocator.Find(v.Key);
                    var eventsToSync = await localStore.ReadStream<IEvent>(s, s.Version - v.Value + 1, v.Value).ToList();
                    if (eventsToSync.Count <= 0) 
                        continue;
                    
                    var remoteStream = remoteStreams.SingleOrDefault(x => x.Id == s.Id);
                    if (remoteStream == default)
                        remoteStream = new Stream(s.Id, s.Type, ExpectedVersion.NoStream, branchId);
                    await remoteStore.AppendToStream(remoteStream, eventsToSync, false);

                    pushResult.NumberOfMessages += eventsToSync.Count;
                    pushResult.NumberOfStreams++;
                }

                foreach (var c in commandChanges)
                    _remoteCommandLog.AppendCommand(c);
                pushResult.NumberOfMessages += commandChanges.Count();
                pushResult.NumberOfStreams += commandChanges.Select(c => c.EventType).Distinct().Count();

                await _branchManager.DeleteBranch(syncRemoteTimeline);

                _log.Info($"Pushed {pushResult.NumberOfMessages} objects to {pushResult.NumberOfStreams} streams on branch {branchId}");
            }
            
            return pushResult;
        }

        /// <inheritdoc />
        public async Task<FastForwardResult> Pull(string branchId)
        {
            var pullResult = new FastForwardResult() { ResultStatus = FastForwardResult.Status.Failed };

            var syncTime = _branchManager.GetTime(_branchManager.ActiveBranch);
            var syncRemoteTimeline = $"{branchId}-{ReplicaName}-{syncTime.ToUnixTimeMilliseconds()}";

            await CopyRemoteToTimeline<IAggregate>(branchId, syncRemoteTimeline);
            await CopyRemoteToTimeline<ISaga>(branchId, syncRemoteTimeline);
           
            var mergeResult = await _branchManager.Merge(syncRemoteTimeline);
            if (mergeResult.success)
                pullResult.ResultStatus = FastForwardResult.Status.Success;
            _log.Info($"Pulled {pullResult.NumberOfMessages} objects to {pullResult.NumberOfStreams} streams on branch {branchId}");
            return pullResult;
        }

        private async Task CopyRemoteToTimeline<TEventSourced>(string branchId, string syncRemoteTimeline)
            where TEventSourced : IEventSourced 
        {
            var timeline = _branchManager.ActiveBranch;
            // await _branchManager.Branch(syncRemoteTimeline);
            var localStore = GetEventStore<TEventSourced>(false);
            var remoteStore = GetEventStore<TEventSourced>(true);
            var remoteStreams = await remoteStore.ListStreams(branchId).ToList();

            await _branchManager.Branch(syncRemoteTimeline, Time.Default);
            foreach (var s in remoteStreams)
            {
                var remoteEvents = await remoteStore.ReadStream<IEvent>(s, 0).ToList();
                var localStream = s.Branch(syncRemoteTimeline, ExpectedVersion.EmptyStream);
                await localStore.AppendToStream(localStream, remoteEvents, false);
            }

            await _branchManager.Branch(timeline);
        }
        
        private IEventStore GetEventStore<TEventSourced>(bool useRemote)
            where TEventSourced : IEventSourced
        {
            if (typeof(TEventSourced) == typeof(IAggregate))
                return useRemote ? _remoteAggregateStore : _aggregateStore;
            if (typeof(TEventSourced) == typeof(ISaga))
                return useRemote ? _remoteSagaStore : _sagaStore;
            return null;
        }
        
        private IEventStore GetEventStore(IStream stream, bool useRemote)
        {
            if (stream.IsSaga)
                return useRemote ? _remoteSagaStore : _sagaStore;
            else
                return useRemote ? _remoteAggregateStore : _aggregateStore;
        }
    }
}