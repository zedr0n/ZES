namespace ZES.Interfaces.Causality
{
    /// <summary>
    /// Graph read processing states
    /// </summary>
    public enum GraphReadState
    {
        /// <summary>
        /// Graph reading is idle
        /// </summary>
        Sleeping,
        
        /// <summary>
        /// Graph reads are being paused
        /// </summary>
        Pausing,
        
        /// <summary>
        /// Reads queued 
        /// </summary>
        Queued,
        
        /// <summary>
        /// Reads are being executed
        /// </summary>
        Reading
    }
}