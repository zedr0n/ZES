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
        /// Do validation for stream events insertion
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="version">Version to insert at</param>
        /// <param name="events">Events to insert</param>
        /// <returns>True if can insert in valid way</returns>
        Task<bool> CanInsertIntoStream(IStream stream, int version, IEnumerable<IEvent> events);

        /// <summary>
        /// Insert events into stream at version
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="version">Version to insert at</param>
        /// <param name="events">Events to insert</param>
        /// <returns>Task completes when the events are inserted</returns>
        Task<bool> TryInsertIntoStream(IStream stream, int version, IEnumerable<IEvent> events);

        /// <summary>
        /// Trim the stream removing events after the version
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="version">Last version to keep</param>
        /// <returns>Task completes when the stream is trimmed</returns>
        Task TrimStream(IStream stream, int version);

        Task<bool> CanDelete(IStream stream, int version);
        Task<bool> TryDelete(IStream stream, int version);
    }
}