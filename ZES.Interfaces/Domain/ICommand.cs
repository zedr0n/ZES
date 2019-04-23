namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Command definition
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Gets aggregate target id
        /// </summary>
        /// <value>
        /// Aggregate target id
        /// </value>
        string Target { get;  }

        /// <summary>
        /// Gets command timestamp
        /// </summary>
        /// <value>
        /// Command timestamp
        /// </value>
        long? Timestamp { get;  }
    }
}