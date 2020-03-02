using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;
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
        /// <returns>List of invalid events, null if none</returns>
        Task<IEnumerable<IEvent>> ValidateInsert(IStream stream, int version, IEnumerable<IEvent> events);

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

        /// <summary>
        /// Do validation for event deletion
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="version">Event version to delete</param>
        /// <returns>Invalid events if any</returns>
        Task<IEnumerable<IEvent>> ValidateDelete(IStream stream, int version);
        
        /// <summary>
        /// Delete event from the stream
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="version">Event version to delete</param>
        /// <returns>True if deletion is valid</returns>
        Task<bool> TryDelete(IStream stream, int version);

        /// <summary>
        /// Get changes caused by the command
        /// </summary>
        /// <param name="command">Command to process</param>
        /// <param name="time">Time command  is applied</param>
        /// <returns>List of events by stream</returns>
        Task<Dictionary<IStream, IEnumerable<IEvent>>> GetChanges(ICommand command, long time);

        /// <summary>
        /// Replay the command retroactively
        /// </summary>
        /// <param name="c">Command to replay</param>
        /// <returns>True if replay is successful</returns>
        Task<bool> ReplayCommand(ICommand c);
        
        /// <summary>
        /// Rollback the commands removing the resulting events
        /// </summary>
        /// <param name="commands">Commands to rollback</param>
        /// <returns>True if rollback is successful</returns>
        Task<bool> RollbackCommands(IEnumerable<ICommand> commands);

        /// <summary>
        /// Validate changes for inserting event
        /// </summary>
        /// <param name="changes">Changes to apply</param>
        /// <param name="time">Time to apply</param>
        /// <returns>List of invalid events if any</returns>
        Task<IEnumerable<IEvent>> ValidateInsert(Dictionary<IStream, IEnumerable<IEvent>> changes, long time);
        
        /// <summary>
        /// Apply changes  
        /// </summary>
        /// <param name="changes">Changes to apply</param>
        /// <param name="time">Time to apply</param>
        /// <returns>True if successfully applied</returns>
        Task<bool> TryInsertIntoStream(Dictionary<IStream, IEnumerable<IEvent>> changes, long time);
    }
}