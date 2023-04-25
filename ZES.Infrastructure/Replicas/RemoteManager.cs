using System.Collections.Concurrent;
using System.Collections.Generic;
using ZES.Infrastructure.Branching;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Replicas;

namespace ZES.Infrastructure.Replicas
{
    /// <inheritdoc />
    public class RemoteManager : IRemoteManager
    {
        private readonly IEventStore<IAggregate> _aggregateStore;
        private readonly IEventStore<ISaga> _sagaStore;
        private readonly ICommandLog _commandLog;
        private readonly ILog _log;
        private readonly IBranchManager _branchManager;
        private readonly IStreamLocator _streamLocator;
        private readonly IClock _clock;
        private readonly ConcurrentDictionary<string, IRemote> _remotes;
        private readonly Dictionary<string, IEventStore<IAggregate>> _aggregateEventStores;
        private readonly Dictionary<string, IEventStore<ISaga>> _sagaEventStores;
        private readonly Dictionary<string, ICommandLog> _commandLogs;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteManager"/> class.
        /// </summary>
        /// <param name="aggregateStore">Local aggregate store</param>
        /// <param name="sagaStore">Local saga store </param>
        /// <param name="commandLog">Command log</param>
        /// <param name="log">Logging service</param>
        /// <param name="branchManager">Branch manager service</param>
        /// <param name="streamLocator">Stream locator service</param>
        /// <param name="clock">Clock instance</param>
        public RemoteManager(
            IEventStore<IAggregate> aggregateStore, 
            IEventStore<ISaga> sagaStore, 
            ICommandLog commandLog,
            ILog log,
            IBranchManager branchManager,
            IStreamLocator streamLocator,
            IClock clock)
        {
            _aggregateStore = aggregateStore;
            _sagaStore = sagaStore;
            _commandLog = commandLog;
            _log = log;
            _branchManager = branchManager;
            _streamLocator = streamLocator;
            _clock = clock;
            _aggregateEventStores = new Dictionary<string, IEventStore<IAggregate>>();
            _sagaEventStores = new Dictionary<string, IEventStore<ISaga>>();
            _commandLogs = new Dictionary<string, ICommandLog>();
            _remotes = new ConcurrentDictionary<string, IRemote>();
        }

        /// <inheritdoc />
        public IRemote GetGenericRemote(string replicaName)
        {
            var aggregateStore = GetAggregateStore(replicaName);
            var sagaStore = GetSagaStore(replicaName);
            var commandLog = GetCommandLog(replicaName);
            if (aggregateStore == null || sagaStore == null || commandLog == null)
                return null;
            return _remotes.GetOrAdd(
                replicaName, 
                s => 
                    new GenericRemote(
                        s, 
                        _aggregateStore, 
                        _sagaStore, 
                        _commandLog,
                        aggregateStore, 
                        sagaStore,
                        commandLog,
                        _log,
                        _branchManager,
                        _streamLocator, 
                        _clock));
        }

        /// <inheritdoc />
        public IEventStore<IAggregate> GetAggregateStore(string replicaName)
        {
            if (_aggregateEventStores.ContainsKey(replicaName))
                return _aggregateEventStores[replicaName];
            return null;
        }

        /// <inheritdoc />
        public IEventStore<ISaga> GetSagaStore(string replicaName)
        {
            if (_sagaEventStores.ContainsKey(replicaName))
                return _sagaEventStores[replicaName];
            return null;
        }

        /// <inheritdoc />
        public ICommandLog GetCommandLog(string replicaName)
        {
            if (_commandLogs.ContainsKey(replicaName))
                return _commandLogs[replicaName];
            return null;
        }

        /// <inheritdoc />
        public bool RegisterLocalReplica(
            string replicaName,
            IEventStore<IAggregate> aggregateStore,
            IEventStore<ISaga> sagaStore,
            ICommandLog commandLog)
        {
            _aggregateEventStores[replicaName] = aggregateStore;
            _sagaEventStores[replicaName] = sagaStore;
            _commandLogs[replicaName] = commandLog;
            return true;
        }
    }
}