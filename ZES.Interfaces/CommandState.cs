namespace ZES.Interfaces
{
    /// <summary>
    /// Command state enum
    /// </summary>
    public enum CommandState
    {
        /// <summary>
        /// Command currently executing
        /// </summary>
        Executing,

        /// <summary>
        /// Command complete with all derived events
        /// </summary>
        Complete,

        /// <summary>
        /// Command failed
        /// </summary>
        Failed,

        /// <summary>
        /// Command not found
        /// </summary>
        NotFound,
    }
}