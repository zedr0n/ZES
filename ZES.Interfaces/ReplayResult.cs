namespace ZES.Interfaces
{
    /// <inheritdoc />
    public class ReplayResult : IReplayResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReplayResult"/> class.
        /// </summary>
        /// <param name="elapsed">Replay length</param>
        public ReplayResult(long elapsed)
        {
            Elapsed = elapsed;
        }

        /// <inheritdoc />
        public string Difference { get; set; } 

        /// <inheritdoc />
        public string Output { get; set; }

        /// <inheritdoc />
        public long Elapsed { get; }

        /// <inheritdoc />
        public bool Result { get; set; }
    }
}