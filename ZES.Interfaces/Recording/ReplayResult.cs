namespace ZES.Interfaces.Recording
{
    /// <inheritdoc />
    public sealed record ReplayResult(long Elapsed) : IReplayResult
    {
        /// <inheritdoc />
        public string Difference { get; set; } 

        /// <inheritdoc />
        public string Output { get; set; }

        /// <inheritdoc />
        public bool Result { get; set; }
    }
}