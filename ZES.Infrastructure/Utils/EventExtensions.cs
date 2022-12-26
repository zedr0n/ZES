using ZES.Infrastructure.EventStore;
using ZES.Interfaces;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Event extensions 
    /// </summary>
    public static class EventExtensions
    {
        /// <summary>
        /// Get the aggregate root id for the event
        /// </summary>
        /// <param name="e">Event instance</param>
        /// <returns>Aggregate root id</returns>
        public static string AggregateRootId(this IEvent e)
        {
            var stream = e.OriginatingStream ?? e.Stream;
            return new Stream(stream).Id;
        }
    }
}