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
    }
}