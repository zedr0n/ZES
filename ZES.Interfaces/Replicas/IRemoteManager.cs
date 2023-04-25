using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces.Replicas
{
    /// <summary>
    /// Remote replica manager
    /// </summary>
    public interface IRemoteManager
    {
        /// <summary>
        /// Gets the generic remote for replica name
        /// </summary>
        /// <param name="replicaName">Replica name</param>
        /// <returns>Generic remote instance</returns>
        IRemote GetGenericRemote(string replicaName);
        
        /// <summary>
        /// Gets the remote aggregate store
        /// </summary>
        /// <param name="replicaName">Replica name</param>
        /// <returns>Remote aggregate event store instance</returns>
        IEventStore<IAggregate> GetAggregateStore(string replicaName);
        
        /// <summary>
        /// Gets the remote saga store
        /// </summary>
        /// <param name="replicaName">Replica name</param>
        /// <returns>Remote saga store instance</returns>
        IEventStore<ISaga> GetSagaStore(string replicaName);
        
        /// <summary>
        /// Gets the remote command log
        /// </summary>
        /// <param name="replicaName">Replica name</param>
        /// <returns>Remote command log instance</returns>
        ICommandLog GetCommandLog(string replicaName);

        /// <summary>
        /// Registers the local replica instance
        /// </summary>
        /// <param name="replicaName">Replica name</param>
        /// <param name="aggregateStore">Aggregate store</param>
        /// <param name="sagaStore">Saga store</param>
        /// <param name="commandLog">Command log</param>
        /// <returns>True if successful</returns>
        bool RegisterLocalReplica(string replicaName, IEventStore<IAggregate> aggregateStore, IEventStore<ISaga> sagaStore, ICommandLog commandLog);
    }
}