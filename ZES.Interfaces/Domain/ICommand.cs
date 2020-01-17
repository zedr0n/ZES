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
        /// <value>
        /// Aggregate target id
        /// </value>
        string Target { get;  }

        /// <summary>
        /// Gets aggregate root type
        /// </summary>
        /// <value>
        /// Aggregate root type
        /// </value>
        string RootType { get; }
        
        /// <summary>
        /// Gets a value indicating whether to use timestamp for aggregate events
        /// </summary>
        bool UseTimestamp { get; }
    }

    /// <inheritdoc />
    public interface ICreateCommand : ICommand { }
}