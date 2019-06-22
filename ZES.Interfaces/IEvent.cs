namespace ZES.Interfaces
{
    /// <summary>
    /// Base event
    /// </summary>
    public interface IEvent : IEventMetadata
    {
        /// <summary>
        /// Gets event type
        /// </summary>
        /// <value>
        /// Event type
        /// </value>
        string EventType { get; }

        /// <summary>
        /// Gets originating stream key
        /// </summary>
        /// <value>
        /// Originating stream key
        /// </value>
        string Stream { get;  }
    }
}