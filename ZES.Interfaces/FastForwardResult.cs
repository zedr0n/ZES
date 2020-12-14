namespace ZES.Interfaces
{
    /// <summary>
    /// Push result
    /// </summary>
    public class FastForwardResult
    {
        /// <summary>
        /// Push result status
        /// </summary>
        public enum Status
        {
            /// <summary>
            /// Fast forward failed 
            /// </summary>
            Failed = -1,
            
            /// <summary>
            /// Fast forward succeeded 
            /// </summary>
            Success = 0,
        }

        /// <summary>
        /// Gets or sets number of streams updated during sync
        /// </summary>
        public int NumberOfStreams { get; set; }

        /// <summary>
        /// Gets or sets the number of objects updated during sync
        /// </summary>
        public int NumberOfMessages { get; set; }

        /// <summary>
        /// Gets or sets sync result status 
        /// </summary>
        public Status ResultStatus { get; set; } = Status.Failed;
    }
}