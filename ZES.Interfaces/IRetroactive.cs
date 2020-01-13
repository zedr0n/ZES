using System.Collections.Generic;
using System.Threading.Tasks;
using ZES.Interfaces.EventStore;

namespace ZES.Interfaces
{
    /// <summary>
    /// Retroactive manipulations
    /// </summary>
    public interface IRetroactive
    {
        /// <summary>
        /// Insert events into stream at version
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="version">Version to insert at</param>
        /// <param name="events">Events to insert</param>
        /// <returns>Task completes when the events are inserted</returns>
        Task InsertIntoStream(IStream stream, int version, IEnumerable<IEvent> events);
    }
}