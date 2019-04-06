namespace ZES.Interfaces.Domain
{
    public interface ICommand
    {
        /// <summary>
        /// Aggregate target id
        /// </summary>
        string Target { get;  }
        /// <summary>
        /// Unix time offset for command timestamp
        /// </summary>
        long Timestamp { get;  }
    }
    
    public interface ISideEffectCommand {}
}