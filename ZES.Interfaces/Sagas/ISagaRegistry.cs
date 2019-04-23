using System;

namespace ZES.Interfaces.Sagas
{
    /// <summary>
    /// Registry mapping events to consuming sagas
    /// </summary>
    public interface ISagaRegistry
    {
        /// <summary>
        /// Gets the mapping from event to saga id
        /// </summary>
        /// <typeparam name="TSaga">Saga type</typeparam>
        /// <returns>Mapping from event to saga identifier</returns>
        Func<IEvent, string> SagaId<TSaga>();
    }
}