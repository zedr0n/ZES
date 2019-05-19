namespace ZES.Interfaces
{
    /// <summary>
    /// Push result
    /// </summary>
    public class PushResult
    {
        /// <summary>
        /// Push result status
        /// </summary>
        public enum PushResultStatus
        {
            /// <summary>
            /// Push failed 
            /// </summary>
            Failed = -1,
            Success = 0
        }
        
        public int NumberOfStreams { get; set; }
        public int NumberOfMessages { get; set; }

        public PushResultStatus Status { get; set; } = PushResultStatus.Failed;
    }
}