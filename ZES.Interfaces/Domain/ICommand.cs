namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Command definition
    /// </summary>
    public interface ICommand : IMessage
    {
        /// <summary>
        /// Gets aggregate target id
        /// </summary>
        string Target { get; }

        /// <summary>
        /// Gets resulting event type 
        /// </summary>
        string EventType { get; }

        /// <summary>
        /// Gets or sets a value indicating whether to use timestamp for aggregate events
        /// </summary>
        bool UseTimestamp { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to store the command in the log
        /// </summary>
        bool StoreInLog { get; set; }
    }

    /// <inheritdoc />
    public interface ICreateCommand : ICommand { }
}