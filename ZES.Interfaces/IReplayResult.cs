namespace ZES.Interfaces
{
    /// <summary>
    /// Replay result
    /// </summary>
    public interface IReplayResult
    {
        /// <summary>
        /// Gets the replay time 
        /// </summary>
        long Elapsed { get; }
        
        /// <summary>
        /// Gets a value indicating whether the replay gives the same result
        /// </summary>
        bool Result { get; }
        
        /// <summary>
        /// Gets the new record log output
        /// </summary>
        string Output { get; }

        /// <summary>
        /// Gets the difference if any
        /// </summary>
        string Difference { get; }
    }
}