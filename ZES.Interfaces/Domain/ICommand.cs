namespace ZES.Interfaces.Domain
{
    public interface ICommand
    {
        /// <summary>
        /// Gets aggregate target id
        /// </summary>
        string Target { get;  }

        /// <summary>
        /// Unix time offset for command timestamp
        /// </summary>
        long? Timestamp { get;  }
    }
}